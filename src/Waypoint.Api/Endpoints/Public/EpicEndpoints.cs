using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

// Epics = the "module" grouping a board can be organized by (vs. by sprint/cycle).
public static class EpicEndpoints
{
    public static void MapEpicEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/epics");

        group.MapGet("/", async (string slug, IProjectService projects, WaypointDbContext db,
            HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var list = await db.Set<Epic>().AsNoTracking()
                .Where(e => e.ProjectId == project.Id)
                .OrderBy(e => e.SequenceId)
                .Select(e => new EpicDto(e.Id, e.SequenceId, e.Title, e.Status))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/", async (string slug, CreateEpicRequest req, IProjectService projects,
            WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            if (string.IsNullOrWhiteSpace(req.Title))
                throw new ValidationException("title_required", "An epic title is required.");
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var maxSeq = await db.Set<Epic>().Where(e => e.ProjectId == project.Id)
                .MaxAsync(e => (int?)e.SequenceId, ct) ?? 0;
            var epic = new Epic
            {
                ProjectId = project.Id, SequenceId = maxSeq + 1, Title = req.Title.Trim(), Status = "planned",
            };
            db.Set<Epic>().Add(epic);
            await db.SaveChangesAsync(ct);
            return Results.Created($"{projectsPrefix}/{slug}/epics/{epic.SequenceId}",
                new EpicDto(epic.Id, epic.SequenceId, epic.Title, epic.Status));
        });
    }
}
