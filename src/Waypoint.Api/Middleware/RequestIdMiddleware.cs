namespace Waypoint.Api.Middleware;

public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-Id";
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var requestId = ctx.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString();

        ctx.Items["RequestId"] = requestId;
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        await _next(ctx);
    }
}
