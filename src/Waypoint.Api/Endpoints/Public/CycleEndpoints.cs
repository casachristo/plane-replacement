using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Planning.Cycles;

namespace Waypoint.Api.Endpoints.PublicApi;

// Cycles = the "sprint/milestone" grouping a board can be organized by (vs. by epic/module).
// Read surface only for now; creating cycles has no API yet (tracked as a follow-up).
public static class CycleEndpoints
{
    public static void MapCycleEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/cycles");

        group.MapGet("/", async (string slug, ICycleService cycles, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            return Results.Ok(await cycles.ListAsync(slug, ct));
        });
    }
}
