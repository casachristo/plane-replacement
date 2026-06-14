using System.Collections.Concurrent;
using Waypoint.Api.Auth;

namespace Waypoint.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly ConcurrentDictionary<string, CachedResponse> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    // Hard cap on stored entries — prevents unbounded growth even if SweepExpired never runs.
    // Each entry holds the full response body, so 10k entries * ~10KB avg = ~100MB ceiling.
    private const int MaxEntries = 10_000;
    private const char KeySeparator = '\n';
    private static long _opsSinceSweep;

    private readonly RequestDelegate _next;
    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    // WAY-26: the cache key incorporates the calling principal, the method, and the normalized
    // path in addition to the Idempotency-Key header. Keying on the header ALONE leaked one
    // caller's cached body to a different caller (or a different endpoint) that reused the same
    // key — e.g. a minted-token response replayed to an unrelated request. Runs AFTER
    // PrincipalMiddleware (see Program.cs) so the principal is resolved.
    private static string BuildKey(HttpContext ctx, string idempotencyKey)
    {
        var principalId = ctx.GetPrincipal()?.Id ?? "anon";
        var method = ctx.Request.Method;
        var path = (ctx.Request.Path.Value ?? string.Empty).TrimEnd('/').ToLowerInvariant();
        // Newline-separated: none of the components can contain a newline, so the key is unambiguous.
        return string.Join(KeySeparator, principalId, method, path, idempotencyKey);
    }

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

        var k = BuildKey(ctx, key.ToString());
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
        // WAY-26: never cache a 5xx — server errors are transient and must not be replayed for
        // 24h. Only persist a final (success or deterministic 4xx) response under the key.
        if (ctx.Response.StatusCode < 500)
            _cache[k] = new CachedResponse(ctx.Response.StatusCode, ctx.Response.ContentType ?? "application/json", bytes, DateTimeOffset.UtcNow + Ttl);
        await originalBody.WriteAsync(bytes);
        ctx.Response.Body = originalBody;

        // Opportunistic eviction: every 100 stores, sweep expired entries. Also evict
        // hardest-aged entries if we have exceeded MaxEntries.
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
