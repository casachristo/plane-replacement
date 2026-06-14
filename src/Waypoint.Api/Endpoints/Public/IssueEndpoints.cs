using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Pagination;
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class IssueEndpoints
{
    public static void MapIssueEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues");

        group.MapPost("/", async (string slug, CreateIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await issues.CreateAsync(project.Id, req.Title, req.DescriptionMd, req.IssueTypeId, req.EpicId, req.CycleId, ct);
            var withIncludes = await issues.GetBySequenceAsync(project.Id, issue.SequenceId, ct);
            return Results.Created($"{projectsPrefix}/{slug}/issues/{issue.SequenceId}", ToDto(withIncludes!));
        });

        group.MapGet("/{seq:int}", async (string slug, int seq,
            IProjectRepository projects, IIssueRepository issues, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await issues.GetBySequenceAsync(project.Id, seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var ac = await db.Set<AcceptanceCriterion>().AsNoTracking()
                .Where(a => a.IssueId == issue.Id)
                .OrderBy(a => a.Position)
                .ToListAsync(ct);
            var acDtos = ac.Select(a => new AcceptanceCriterionDto(
                a.Id, a.Position, a.Text, a.Checked, a.CheckedAt,
                a.CheckedByActorType?.ToString(),
                a.CheckedByActorId,
                a.CheckedByActorLabel,
                a.CreatedAt, a.UpdatedAt)).ToList();
            return Results.Ok(ToDto(issue) with { AcceptanceCriteria = acDtos });
        });

        group.MapPatch("/{seq:int}", async (string slug, int seq, UpdateIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var updated = await issues.UpdateAsync(project.Id, seq, req.Title, req.DescriptionMd, req.Priority, ct);
            return Results.Ok(ToDto(updated));
        });

        // Assign (or unassign, with epicId=null) an issue's module/epic — the dimension the
        // board can group by.
        group.MapPut("/{seq:int}/epic", async (string slug, int seq, AssignEpicRequest req,
            IProjectRepository projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            if (req.EpicId is { } epicId &&
                !await db.Set<Epic>().AnyAsync(e => e.Id == epicId && e.ProjectId == project.Id, ct))
                throw new NotFoundException("epic_not_found", "Epic not found in this project.");
            var issue = await db.Issues.Include(i => i.State).Include(i => i.IssueType)
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            issue.EpicId = req.EpicId;
            await db.SaveChangesAsync(ct);
            if (issue.EpicId is not null) await db.Entry(issue).Reference(i => i.Epic).LoadAsync(ct);
            return Results.Ok(ToDto(issue));
        });

        // Assign (or unassign, with cycleId=null) an issue's cycle/milestone — the sprint
        // dimension, parallel to /epic above.
        group.MapPut("/{seq:int}/cycle", async (string slug, int seq, AssignCycleRequest req,
            IProjectRepository projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            if (req.CycleId is { } cycleId &&
                !await db.Set<Cycle>().AnyAsync(c => c.Id == cycleId && c.ProjectId == project.Id, ct))
                throw new NotFoundException("cycle_not_found", "Cycle not found in this project.");
            var issue = await db.Issues.Include(i => i.State).Include(i => i.IssueType).Include(i => i.Epic)
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            issue.CycleId = req.CycleId;
            await db.SaveChangesAsync(ct);
            if (issue.CycleId is not null) await db.Entry(issue).Reference(i => i.Cycle).LoadAsync(ct);
            return Results.Ok(ToDto(issue));
        });

        group.MapPost("/{seq:int}/transitions", async (string slug, int seq, TransitionIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            // WAY-19: writer tokens can edit fields but not move state — 403 unless the caller
            // holds issue:transition (or admin). Cairn's transition uses an admin token.
            AuthGuard.RequireTransitionRights(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            if (req.Force && string.IsNullOrWhiteSpace(req.BypassReason))
                throw new ValidationException("bypass_reason_required", "force=true requires a non-empty BypassReason.");
            var principal = ctx.GetPrincipal();
            var updated = await issues.TransitionAsync(project.Id, seq, req.ToStateId, req.Force, req.BypassReason, principal, ct);
            return Results.Ok(ToDto(updated));
        });

        group.MapGet("/", async (string slug, int? limit, string? cursor,
            IProjectRepository projects, IIssueRepository issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var pageSize = Math.Clamp(limit ?? IssueRepository.DefaultPageSize, 1, IssueRepository.MaxPageSize);
            var (items, total) = await issues.ListAsync(project.Id, pageSize, cursor, ct);
            string? nextCursor = null;
            if (items.Count == pageSize)
            {
                var last = items[^1];
                nextCursor = Cursor.Encode(last.CreatedAt, last.Id);
            }
            var data = items.Select(ToDto).ToList();
            return Results.Ok(new PagedResponse<IssueDto>(data, nextCursor, total));
        });

        group.MapGet("/{seq:int}/activity", async (string slug, int seq,
            IProjectRepository projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var events = await db.Activities.AsNoTracking()
                .Where(a => a.IssueId == issue.Id)
                .OrderBy(a => a.At)
                .Select(a => new ActivityDto(a.Id, a.ActorType.ToString(), a.ActorId, a.ActorLabel,
                    a.Verb, a.BeforeJson, a.AfterJson, a.At))
                .ToListAsync(ct);
            return Results.Ok(events);
        });
    }

    private static IssueDto ToDto(Issue i) => new(
        i.Id, i.SequenceId, i.Title, i.DescriptionMd,
        i.StateId, i.State.Name,
        i.IssueTypeId, i.IssueType.Name,
        (int)i.Priority,
        i.EpicId, i.Epic?.Title,   // module (epic): EpicId always set; Title only when Epic is included
        i.CycleId, i.Cycle?.Name,  // milestone (cycle): same include rule as Epic
        i.CreatedAt, i.UpdatedAt);
}
