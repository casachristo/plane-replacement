namespace Waypoint.Api.Auth;

public interface IPrincipalResolver
{
    Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct);
}
