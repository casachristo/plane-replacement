using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Planning.Epics;
using Waypoint.Contracts;

namespace Waypoint.Api.Endpoints.PublicApi;

// Epics = the "module" grouping a board can be organized by (vs. by sprint/cycle).
public static class EpicEndpoints
{
    public static void MapEpicEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/epics");

        group.MapGet("/", async (string slug, IEpicService epics, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            return Results.Ok(await epics.ListAsync(slug, ct));
        });

        group.MapPost("/", async (string slug, CreateEpicRequest req, IEpicService epics, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var epic = await epics.CreateAsync(slug, req, ct);
            return Results.Created($"{projectsPrefix}/{slug}/epics/{epic.Sequence}", epic);
        });
    }
}
