using System.Collections.Concurrent;

namespace Waypoint.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly ConcurrentDictionary<string, CachedResponse> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

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
    }

    private sealed record CachedResponse(int StatusCode, string ContentType, byte[] Body, DateTimeOffset ExpiresAt);
}
