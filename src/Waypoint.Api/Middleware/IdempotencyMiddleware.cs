using System.Collections.Concurrent;

namespace Waypoint.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly ConcurrentDictionary<string, CachedResponse> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    // Hard cap on stored entries — prevents unbounded growth even if SweepExpired never runs.
    // Each entry holds the full response body, so 10k entries * ~10KB avg = ~100MB ceiling.
    private const int MaxEntries = 10_000;
    private static long _opsSinceSweep;

    private readonly RequestDelegate _next;
    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method) && !HttpMethods.IsPatch(ctx.Request.Method))
        {
            await _next(ctx);
            return;
        }
        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var key) || string.IsNullOrWhiteSpace(key))
        {
            await _next(ctx);
            return;
        }

        var k = key.ToString();
        if (_cache.TryGetValue(k, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            ctx.Response.StatusCode = cached.StatusCode;
            ctx.Response.ContentType = cached.ContentType;
            await ctx.Response.Body.WriteAsync(cached.Body);
            return;
        }

        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        await _next(ctx);
        buffer.Position = 0;
        var bytes = buffer.ToArray();
        _cache[k] = new CachedResponse(ctx.Response.StatusCode, ctx.Response.ContentType ?? "application/json", bytes, DateTimeOffset.UtcNow + Ttl);
        await originalBody.WriteAsync(bytes);
        ctx.Response.Body = originalBody;

        // Opportunistic eviction: every 100 stores, sweep expired entries. Also evict
        // hardest-aged entries if we've exceeded MaxEntries.
        if (Interlocked.Increment(ref _opsSinceSweep) % 100 == 0)
        {
            SweepExpired();
        }
    }

    private static void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt <= now) _cache.TryRemove(kvp.Key, out _);
        }
        // If still over cap (e.g. lots of within-TTL entries), drop the oldest.
        if (_cache.Count > MaxEntries)
        {
            var oldest = _cache.OrderBy(kvp => kvp.Value.ExpiresAt).Take(_cache.Count - MaxEntries).ToList();
            foreach (var kvp in oldest) _cache.TryRemove(kvp.Key, out _);
        }
    }

    private sealed record CachedResponse(int StatusCode, string ContentType, byte[] Body, DateTimeOffset ExpiresAt);
}
