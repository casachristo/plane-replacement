namespace Waypoint.Api.Auth;

/// <summary>
/// Runs the registered IPrincipalResolver and stashes the result in HttpContext.Items["Principal"].
/// Downstream code reads via HttpContextExtensions.GetPrincipal().
/// </summary>
public sealed class PrincipalMiddleware
{
    public const string ItemKey = "Principal";
    private readonly RequestDelegate _next;
    private readonly IPrincipalResolver _resolver;

    public PrincipalMiddleware(RequestDelegate next, IPrincipalResolver resolver)
    {
        _next = next;
        _resolver = resolver;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var principal = await _resolver.ResolveAsync(ctx, ctx.RequestAborted);
        if (principal is not null) ctx.Items[ItemKey] = principal;
        await _next(ctx);
    }
}

public static class HttpContextExtensions
{
    public static Principal? GetPrincipal(this HttpContext ctx)
        => ctx.Items.TryGetValue(PrincipalMiddleware.ItemKey, out var p) ? p as Principal : null;
}
