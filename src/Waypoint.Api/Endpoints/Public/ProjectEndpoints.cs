using Waypoint.Api.Auth;
using Waypoint.Api.Cairn;
using Waypoint.Api.Subsystems.Projects;
using Waypoint.Api.Subsystems.Projects.Labels;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Api.Subsystems.Projects.States;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class ProjectEndpoints
{
    /// <summary>Maps project routes under <paramref name="prefix"/>. Called once with /api/v1/projects (public) and once with /internal/v1/projects (internal).</summary>
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app, string prefix)
    {
        var group = app.MapGroup(prefix);

        group.MapPost("/", async (CreateProjectRequest req, IProjectsOrchestrator projects, HttpContext ctx, CancellationToken ct) =>
        {
            // WAY-5: project creation is an admin-tier operation — a limited (Service) token must
            // not be able to spin up projects. Admin tokens carry the synthetic "admin" scope.
            AuthGuard.RequireScope(ctx, "admin");
            var dto = await projects.ProvisionAsync(req, ct);
            return Results.Created($"{prefix}/{dto.Slug}", dto);
        });

        group.MapGet("/", async (IProjectService projects, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            return Results.Ok(await projects.ListAsync(ct));
        });

        group.MapGet("/{slug}", async (string slug, IProjectService projects, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var dto = await projects.GetAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            return Results.Ok(dto);
        });

        group.MapGet("/{slug}/states", async (string slug, IStateService states, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            return Results.Ok(await states.ListByProjectSlugAsync(slug, ct));
        });

        group.MapGet("/{slug}/labels", async (string slug, ILabelService labels, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            return Results.Ok(await labels.ListByProjectSlugAsync(slug, ct));
        });

        // WAY-15: link/unlink a project to a Cairn architecture catalog (admin-only config).
        group.MapPut("/{slug}/cairn-link", async (string slug, SetCairnLinkRequest req,
            IProjectService projects, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireScope(ctx, "admin");
            return Results.Ok(await projects.SetCairnLinkAsync(slug, req, ct));
        });

        // WAY-15: the board swimlane rows. Unlinked => CairnLinked=false + no modules
        // (the UI keeps the single-row state Kanban).
        group.MapGet("/{slug}/swimlanes", async (string slug,
            IProjectService projects, ICairnModuleSource cairn, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var p = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            if (string.IsNullOrWhiteSpace(p.CairnProjectName))
                return Results.Ok(new SwimlanesDto(false, System.Array.Empty<string>()));
            var modules = await cairn.GetModulesAsync(p.CairnProjectName, ct);
            return Results.Ok(new SwimlanesDto(true, modules));
        });
    }
}
