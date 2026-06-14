using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Api.Webhooks;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Endpoints.PublicApi;

public static class AcceptanceCriterionEndpoints
{
    public static void MapAcceptanceCriterionEndpoints(this IEndpointRouteBuilder app, string projectsPrefix)
    {
        var group = app.MapGroup($"{projectsPrefix}/{{slug}}/issues/{{seq:int}}/acceptance-criteria");

        group.MapGet("/", async (string slug, int seq,
            IProjectService projects, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireAuth(ctx);
            var issue = await ResolveIssue(slug, seq, projects, db, ct);
            var items = await db.Set<AcceptanceCriterion>().AsNoTracking()
                .Where(a => a.IssueId == issue.Id)
                .OrderBy(a => a.Position)
                .ToListAsync(ct);
            return Results.Ok(items.Select(ToDto).ToList());
        });

        group.MapPost("/", async (string slug, int seq, CreateAcceptanceCriterionRequest req,
            IProjectService projects, WaypointDbContext db, IWebhookPublisher pub, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:write");
            if (string.IsNullOrWhiteSpace(req.Text))
                throw new ValidationException("text_required", "Acceptance criterion text must not be empty.");
            var issue = await ResolveIssue(slug, seq, projects, db, ct);
            var position = req.Position ?? ((await db.Set<AcceptanceCriterion>()
                .Where(a => a.IssueId == issue.Id)
                .MaxAsync(a => (int?)a.Position, ct)) ?? 0) + 1;
            var entity = new AcceptanceCriterion
            {
                IssueId = issue.Id,
                Position = position,
                Text = req.Text.Trim(),
            };
            db.Set<AcceptanceCriterion>().Add(entity);
            await db.SaveChangesAsync(ct);   // assigns entity.Id

            await pub.PublishAsync(WebhookEvent.AcceptanceCriterionCreated, issue.ProjectId,
                WebhookPayloads.AcceptanceCriterion(entity, WebhookPayloads.From(issue)), ct);
            await db.SaveChangesAsync(ct);   // flush delivery rows

            return Results.Created(
                $"{projectsPrefix}/{slug}/issues/{seq}/acceptance-criteria/{entity.Id}",
                ToDto(entity));
        });

        group.MapPatch("/{id:guid}", async (string slug, int seq, Guid id, UpdateAcceptanceCriterionRequest req,
            IProjectService projects, WaypointDbContext db, IWebhookPublisher pub, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:write");
            var issue = await ResolveIssue(slug, seq, projects, db, ct);
            var ac = await db.Set<AcceptanceCriterion>()
                .FirstOrDefaultAsync(a => a.Id == id && a.IssueId == issue.Id, ct)
                ?? throw new NotFoundException("ac_not_found", $"Acceptance criterion {id} not found on issue.");
            if (req.Text is not null)
            {
                if (string.IsNullOrWhiteSpace(req.Text))
                    throw new ValidationException("text_required", "Acceptance criterion text must not be empty.");
                ac.Text = req.Text.Trim();
            }
            if (req.Position is not null) ac.Position = req.Position.Value;
            ac.UpdatedAt = DateTimeOffset.UtcNow;

            await pub.PublishAsync(WebhookEvent.AcceptanceCriterionUpdated, issue.ProjectId,
                WebhookPayloads.AcceptanceCriterion(ac, WebhookPayloads.From(issue)), ct);
            await db.SaveChangesAsync(ct);

            return Results.Ok(ToDto(ac));
        });

        group.MapPost("/{id:guid}/check", async (string slug, int seq, Guid id,
            IProjectService projects, WaypointDbContext db, IWebhookPublisher pub, HttpContext ctx, CancellationToken ct) =>
        {
            var principal = AuthGuard.RequireWriteScope(ctx, "issue:write");
            var issue = await ResolveIssue(slug, seq, projects, db, ct);
            var ac = await db.Set<AcceptanceCriterion>()
                .FirstOrDefaultAsync(a => a.Id == id && a.IssueId == issue.Id, ct)
                ?? throw new NotFoundException("ac_not_found", $"Acceptance criterion {id} not found on issue.");
            ac.Checked = true;
            ac.CheckedAt = DateTimeOffset.UtcNow;
            (ac.CheckedByActorType, ac.CheckedByActorId, ac.CheckedByActorLabel) = ResolveActor(principal);
            ac.UpdatedAt = ac.CheckedAt.Value;

            await pub.PublishAsync(WebhookEvent.AcceptanceCriterionChecked, issue.ProjectId,
                WebhookPayloads.AcceptanceCriterion(ac, WebhookPayloads.From(issue)), ct);
            await db.SaveChangesAsync(ct);

            return Results.Ok(ToDto(ac));
        });

        group.MapPost("/{id:guid}/uncheck", async (string slug, int seq, Guid id,
            IProjectService projects, WaypointDbContext db, IWebhookPublisher pub, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:write");
            var issue = await ResolveIssue(slug, seq, projects, db, ct);
            var ac = await db.Set<AcceptanceCriterion>()
                .FirstOrDefaultAsync(a => a.Id == id && a.IssueId == issue.Id, ct)
                ?? throw new NotFoundException("ac_not_found", $"Acceptance criterion {id} not found on issue.");
            ac.Checked = false;
            ac.CheckedAt = null;
            ac.CheckedByActorType = null;
            ac.CheckedByActorId = null;
            ac.CheckedByActorLabel = null;
            ac.UpdatedAt = DateTimeOffset.UtcNow;

            await pub.PublishAsync(WebhookEvent.AcceptanceCriterionUnchecked, issue.ProjectId,
                WebhookPayloads.AcceptanceCriterion(ac, WebhookPayloads.From(issue)), ct);
            await db.SaveChangesAsync(ct);

            return Results.Ok(ToDto(ac));
        });

        group.MapDelete("/{id:guid}", async (string slug, int seq, Guid id,
            IProjectService projects, WaypointDbContext db, IWebhookPublisher pub, HttpContext ctx, CancellationToken ct) =>
        {
            AuthGuard.RequireWriteScope(ctx, "issue:write");
            var issue = await ResolveIssue(slug, seq, projects, db, ct);
            var ac = await db.Set<AcceptanceCriterion>()
                .FirstOrDefaultAsync(a => a.Id == id && a.IssueId == issue.Id, ct)
                ?? throw new NotFoundException("ac_not_found", $"Acceptance criterion {id} not found on issue.");
            ac.DeletedAt = DateTimeOffset.UtcNow;

            await pub.PublishAsync(WebhookEvent.AcceptanceCriterionDeleted, issue.ProjectId,
                WebhookPayloads.AcceptanceCriterion(ac, WebhookPayloads.From(issue)), ct);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });
    }

    private static async Task<Issue> ResolveIssue(string slug, int seq, IProjectService projects, WaypointDbContext db, CancellationToken ct)
    {
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return await db.Issues.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
            ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
    }

    private static (ActorType, Guid?, string?) ResolveActor(Principal p)
    {
        if (p.PassthroughActorId is not null)
        {
            Guid? id = Guid.TryParse(p.PassthroughActorId, out var g) ? g : null;
            return (ActorType.Passthrough, id, p.PassthroughActorLabel);
        }
        var actorType = p.Kind == PrincipalKind.Human ? ActorType.User : ActorType.Service;
        return (actorType, Guid.TryParse(p.Id, out var pid) ? pid : null, p.DisplayName);
    }

    private static AcceptanceCriterionDto ToDto(AcceptanceCriterion a) => new(
        a.Id, a.Position, a.Text, a.Checked, a.CheckedAt,
        a.CheckedByActorType?.ToString(),
        a.CheckedByActorId,
        a.CheckedByActorLabel,
        a.CreatedAt, a.UpdatedAt);
}
