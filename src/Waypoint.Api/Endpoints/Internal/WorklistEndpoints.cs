using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Planning.Worklists;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.InternalApi;

/// <summary>
/// WAY-17: the per-project batch Worklist. Internal surface (:8081) only — Cairn's dispatcher
/// is the sole consumer; the public surface and the Kanban board never expose it.
/// </summary>
public static class WorklistEndpoints
{
    public static void MapWorklistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/v1/projects/{slug}/worklist");

        group.MapGet("/", async (string slug,
            IProjectService projects, IWorklistService worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            return Results.Ok(await worklists.GetAsync(projectId, ct));
        });

        group.MapPost("/start", async (string slug,
            IProjectService projects, IWorklistService worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            return Results.Ok(await worklists.StartAsync(projectId, ct));
        });

        group.MapPost("/advance", async (string slug,
            IProjectService projects, IWorklistService worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            return Results.Ok(await worklists.AdvanceAsync(projectId, ct));
        });

        group.MapPost("/skip", async (string slug, SkipWorklistRequest req,
            IProjectService projects, IWorklistService worklists, HttpContext ctx, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Reason))
                throw new ValidationException("reason_required", "skip requires a non-empty reason.");
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            return Results.Ok(await worklists.SkipAsync(projectId, req.Reason.Trim(), ct));
        });

        group.MapPost("/stop", async (string slug,
            IProjectService projects, IWorklistService worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            return Results.Ok(await worklists.StopAsync(projectId, ct));
        });
    }

    private static async Task<Guid> ResolveProjectId(
        string slug, IProjectService projects, HttpContext ctx, CancellationToken ct)
    {
        var principal = AuthGuard.RequireAuth(ctx);
        if (principal.Kind != PrincipalKind.InternalService)
            throw new ValidationException("internal_service_required", "Worklist endpoints accept service tokens only.");
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return project.Id;
    }
}
