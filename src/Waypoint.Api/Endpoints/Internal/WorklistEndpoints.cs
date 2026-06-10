using Waypoint.Api.Auth;
using Waypoint.Api.Endpoints;
using Waypoint.Api.Repositories;
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
            IProjectRepository projects, IWorklistRepository worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            var (wl, current) = await worklists.GetAsync(projectId, ct);
            return Results.Ok(ToStatus(wl, current));
        });

        group.MapPost("/start", async (string slug,
            IProjectRepository projects, IWorklistRepository worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            var (wl, current) = await worklists.StartAsync(projectId, ct);
            return Results.Ok(ToStatus(wl, current));
        });

        group.MapPost("/advance", async (string slug,
            IProjectRepository projects, IWorklistRepository worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            var (wl, current) = await worklists.AdvanceAsync(projectId, ct);
            return Results.Ok(ToStatus(wl, current));
        });

        group.MapPost("/skip", async (string slug, SkipWorklistRequest req,
            IProjectRepository projects, IWorklistRepository worklists, HttpContext ctx, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Reason))
                throw new ValidationException("reason_required", "skip requires a non-empty reason.");
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            var (wl, current) = await worklists.SkipAsync(projectId, req.Reason.Trim(), ct);
            return Results.Ok(ToStatus(wl, current));
        });

        group.MapPost("/stop", async (string slug,
            IProjectRepository projects, IWorklistRepository worklists, HttpContext ctx, CancellationToken ct) =>
        {
            var projectId = await ResolveProjectId(slug, projects, ctx, ct);
            var wl = await worklists.StopAsync(projectId, ct);
            return Results.Ok(new WorklistStopSummary(wl.DoneCount, wl.SkippedCount, wl.RemainingCount));
        });
    }

    private static async Task<Guid> ResolveProjectId(
        string slug, IProjectRepository projects, HttpContext ctx, CancellationToken ct)
    {
        var principal = AuthGuard.RequireAuth(ctx);
        if (principal.Kind != PrincipalKind.InternalService)
            throw new ValidationException("internal_service_required", "Worklist endpoints accept service tokens only.");
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return project.Id;
    }

    private static WorklistStatusDto ToStatus(Worklist wl, Issue? current) => new(
        State: wl.State.ToString().ToLowerInvariant(),
        Current: current is null ? null : IssueMapper.ToDto(current),
        RemainingCount: wl.RemainingCount,
        DoneCount: wl.DoneCount,
        SkippedCount: wl.SkippedCount,
        StartedAt: wl.StartedAt,
        CompletedAt: wl.CompletedAt);
}
