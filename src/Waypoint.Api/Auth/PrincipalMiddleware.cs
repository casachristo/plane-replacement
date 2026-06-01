namespace Waypoint.Api.Auth;

/// <summary>
/// Runs every registered IPrincipalResolver in DI order and stashes the first non-null
/// result in HttpContext.Items["Principal"]. Downstream code reads via GetPrincipal().
/// </summary>
public sealed class PrincipalMiddleware
{
    public const string ItemKey = "Principal";
    private readonly RequestDelegate _next;

    public PrincipalMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IEnumerable<IPrincipalResolver> resolvers)
    {
        foreach (var resolver in resolvers)
        {
            var principal = await resolver.ResolveAsync(ctx, ctx.RequestAborted);
            if (principal is not null)
            {
                ctx.Items[ItemKey] = principal;
                break;
            }
        }
        await _next(ctx);
    }
}

public static class HttpContextExtensions
{
    public static Principal? GetPrincipal(this HttpContext ctx)
        => ctx.Items.TryGetValue(PrincipalMiddleware.ItemKey, out var p) ? p as Principal : null;
}
