using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Cairn;
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class ProjectEndpoints
{
    /// <summary>Maps project routes under <paramref name="prefix"/>. Called once with /api/v1/projects (public) and once with /internal/v1/projects (internal).</summary>
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app, string prefix)
    {
        var group = app.MapGroup(prefix);

        group.MapPost("/", async (CreateProjectRequest req, IProjectRepository repo, HttpContext ctx, CancellationToken ct) =>
        {
            // WAY-5: project creation is an admin-tier operation — a limited (Service) token must
            // not be able to spin up projects. Admin tokens carry the synthetic "admin" scope.
            AuthGuard.RequireScope(ctx, "admin");
            var p = await repo.CreateAsync(req.Slug, req.Name, req.Identifier, ct);
            var dto = new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt, p.CairnProjectName);
            return Results.Created($"{prefix}/{p.Slug}", dto);
        });

        group.MapGet("/", async (IProjectRepository repo, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var list = await repo.ListAsync(ct);
            return Results.Ok(list.Select(p => new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt, p.CairnProjectName)));
        });

        group.MapGet("/{slug}", async (string slug, IProjectRepository repo, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var p = await repo.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            return Results.Ok(new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt, p.CairnProjectName));
        });

        group.MapGet("/{slug}/states", async (string slug,
            IProjectRepository projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var states = await db.States.AsNoTracking()
                .Where(s => s.ProjectId == project.Id)
                .OrderBy(s => s.SortOrder)
                .Select(s => new StateDto(s.Id, s.Name, s.Group.ToString(), s.Color, s.SortOrder, s.IsDefault))
                .ToListAsync(ct);
            return Results.Ok(states);
        });

        // WAY-15: link/unlink a project to a Cairn architecture catalog (admin-only config).
        group.MapPut("/{slug}/cairn-link", async (string slug, SetCairnLinkRequest req,
            WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireScope(ctx, "admin");
            var p = await db.Projects.FirstOrDefaultAsync(x => x.Slug == slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            p.CairnProjectName = string.IsNullOrWhiteSpace(req.CairnProjectName) ? null : req.CairnProjectName.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt, p.CairnProjectName));
        });

        // WAY-15: the board swimlane rows. Unlinked => CairnLinked=false + no modules
        // (the UI keeps the single-row state Kanban).
        group.MapGet("/{slug}/swimlanes", async (string slug,
            IProjectRepository projects, ICairnModuleSource cairn, HttpContext ctx, CancellationToken ct) =>
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
