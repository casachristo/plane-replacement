using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Repositories;
using Waypoint.Api.Subsystems.Issues;
using Waypoint.Api.Subsystems.Issues.IssueCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

// Thin HTTP adapter over the Issues subsystem (the API layer is exempt from the pattern):
// authenticate and delegate to IIssueService / IIssuesOrchestrator. The epic/cycle assign and
// activity reads remain db-inline pending their feature extraction (WAY-41 continuation).
public static class IssueEndpoints
{
    public static void MapIssueEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues");

        group.MapPost("/", async (string slug, CreateIssueRequest req,
            IIssueService issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:create");
            var dto = await issues.CreateAsync(slug, req, ct);
            return Results.Created($"{projectsPrefix}/{slug}/issues/{dto.Sequence}", dto);
        });

        group.MapGet("/{seq:int}", async (string slug, int seq,
            IIssueService issues, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var issue = await issues.ResolveAsync(slug, seq, ct);
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
            IIssueService issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:write");
            return Results.Ok(await issues.UpdateAsync(slug, seq, req, ct));
        });

        // Assign (or unassign, with epicId=null) an issue's module/epic — the dimension the
        // board can group by.
        group.MapPut("/{seq:int}/epic", async (string slug, int seq, AssignEpicRequest req,
            IProjectRepository projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:write");
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
            AuthGuard.RequireWriteScope(ctx, "issue:write");
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
            IIssuesOrchestrator orchestrator, HttpContext ctx, CancellationToken ct) =>
        {
            // WAY-19: writer tokens can edit fields but not move state — 403 unless the caller
            // holds issue:transition (or admin). Cairn's transition uses an admin token.
            AuthGuard.RequireTransitionRights(ctx);
            return Results.Ok(await orchestrator.TransitionAsync(slug, seq, req, ctx.GetPrincipal(), ct));
        });

        group.MapGet("/", async (string slug, int? limit, string? cursor, string? category,
            IIssueService issues, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            return Results.Ok(await issues.ListAsync(slug, limit, cursor, category, ct));
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

    private static IssueDto ToDto(Issue i) => Waypoint.Api.Endpoints.IssueMapper.ToDto(i);
}
