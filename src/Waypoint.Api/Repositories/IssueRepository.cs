using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Api.Pagination;
using Waypoint.Api.Webhooks;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Repositories;

public sealed class IssueRepository : IIssueRepository
{
    private readonly WaypointDbContext _db;
    private readonly IWebhookPublisher _publisher;
    public IssueRepository(WaypointDbContext db, IWebhookPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<int> NextSequenceAsync(Guid projectId, CancellationToken ct)
    {
        var seqName = $"seq_issues_{projectId:N}";
        var ensure = $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = '{seqName}' AND relkind = 'S') THEN
                    CREATE SEQUENCE {seqName} START 1;
                END IF;
            END $$;
            """;
        await _db.Database.ExecuteSqlRawAsync(ensure, ct);

        var conn = _db.Database.GetDbConnection();
        var opened = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            opened = true;
        }
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT nextval('{seqName}')";
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32((long)result!);
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }

    public async Task<Issue> CreateAsync(Guid projectId, string title, string descriptionMd, Guid? issueTypeId, Guid? epicId, Guid? cycleId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("project_not_found", "Project not found.");
        if (project.DefaultStateId is null)
            throw new ConflictException("project_has_no_default_state", "Project has no default state.");

        var typeId = issueTypeId ?? await _db.IssueTypes
            .Where(t => t.ProjectId == projectId && t.IsDefault)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("project_has_no_default_issue_type", "Project has no default issue type.");

        if (epicId is { } eid && !await _db.Set<Epic>().AnyAsync(e => e.Id == eid && e.ProjectId == projectId, ct))
            throw new NotFoundException("epic_not_found", "Epic not found in this project.");
        if (cycleId is { } cid && !await _db.Set<Cycle>().AnyAsync(c => c.Id == cid && c.ProjectId == projectId, ct))
            throw new NotFoundException("cycle_not_found", "Cycle not found in this project.");

        var seq = await NextSequenceAsync(projectId, ct);
        var issue = new Issue
        {
            ProjectId = projectId,
            SequenceId = seq,
            Title = title,
            DescriptionMd = descriptionMd,
            StateId = project.DefaultStateId.Value,
            IssueTypeId = typeId,
            EpicId = epicId,
            CycleId = cycleId,
        };
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync(ct);

        _db.Activities.Add(new Activity
        {
            IssueId = issue.Id,
            ActorType = ActorType.System,
            Verb = "created",
        });

        var state = await _db.States.AsNoTracking().FirstAsync(s => s.Id == issue.StateId, ct);
        await _publisher.PublishAsync(WebhookEvent.IssueCreated, projectId,
            WebhookPayloads.IssueCreated(issue, state), ct);

        await _db.SaveChangesAsync(ct);
        return issue;
    }

    public Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct) =>
        _db.Issues.AsNoTracking()
            .Include(i => i.State)
            .Include(i => i.IssueType)
            .Include(i => i.Epic)
            .Include(i => i.Cycle)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct);

    public async Task<Issue> TransitionAsync(Guid projectId, int seq, Guid toStateId, bool force, string? bypassReason, Principal? actor, CancellationToken ct)
    {
        var issue = await _db.Issues
            .Include(i => i.State).Include(i => i.IssueType)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct)
            ?? throw new NotFoundException("issue_not_found", "Issue not found.");

        var newState = await _db.States.FindAsync([toStateId], ct)
            ?? throw new NotFoundException("state_not_found", "Target state not found.");
        if (newState.ProjectId != projectId)
            throw new ValidationException("state_wrong_project", "State does not belong to this project.");

        if (issue.StateId == toStateId) return issue;

        var workflowId = issue.IssueType.DefaultWorkflowId
            ?? throw new ConflictException("issue_type_has_no_workflow", "Issue type has no default workflow.");

        var transitionRows = await _db.WorkflowTransitions
            .Where(t => t.WorkflowId == workflowId)
            .Select(t => new { t.FromStateId, t.ToStateId })
            .ToListAsync(ct);
        var transitions = transitionRows.Select(t => (t.FromStateId, t.ToStateId));

        var validator = new Waypoint.Domain.Validation.WorkflowTransitionValidator(transitions);
        if (!validator.CanTransition(issue.StateId, toStateId))
            throw new ConflictException("transition_not_allowed",
                $"Transition from state '{issue.State.Name}' to '{newState.Name}' is not allowed by the workflow.");

        // WAY-4 / WAY-9: pre-fact gate on Completed. Bypass with force=true; every
        // bypass that actually skipped real unchecked AC writes a GateOverrideEvent.
        var gateName = "acceptance_criteria_unchecked";
        GateOverrideEvent? overrideEvent = null;
        if (newState.Group == StateGroup.Completed)
        {
            var unchecked_ = await _db.Set<AcceptanceCriterion>().AsNoTracking()
                .Where(a => a.IssueId == issue.Id && !a.Checked)
                .OrderBy(a => a.Position)
                .Select(a => new { id = a.Id, position = a.Position, text = a.Text })
                .ToListAsync(ct);
            if (unchecked_.Count > 0)
            {
                if (!force)
                {
                    throw new PreconditionFailedException(gateName,
                        $"Cannot transition to '{newState.Name}' — {unchecked_.Count} acceptance criterion(s) are still unchecked.",
                        new Dictionary<string, object> { ["unchecked"] = unchecked_ });
                }
                var (atype, aid, alabel) = ResolveActor(actor);
                overrideEvent = new GateOverrideEvent
                {
                    IssueId = issue.Id,
                    GateName = gateName,
                    Reason = bypassReason ?? string.Empty,
                    ActorType = atype,
                    ActorId = aid,
                    ActorLabel = alabel,
                };
                _db.Set<GateOverrideEvent>().Add(overrideEvent);
            }
        }

        var previousState = issue.State;
        var beforeStateId = issue.StateId;
        issue.StateId = toStateId;
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _db.Activities.Add(new Activity
        {
            IssueId = issue.Id,
            ActorType = ActorType.System,
            Verb = "transitioned",
            BeforeJson = $$"""{"state_id":"{{beforeStateId}}"}""",
            AfterJson = $$"""{"state_id":"{{toStateId}}"}""",
        });

        await _publisher.PublishAsync(WebhookEvent.IssueTransitioned, projectId,
            WebhookPayloads.IssueTransitioned(issue, previousState, newState), ct);
        if (overrideEvent is not null)
        {
            await _publisher.PublishAsync(WebhookEvent.GateOverrideFired, projectId,
                WebhookPayloads.GateOverride(overrideEvent, WebhookPayloads.From(issue)), ct);
        }

        await _db.SaveChangesAsync(ct);

        return await GetBySequenceAsync(projectId, seq, ct)
            ?? throw new InvalidOperationException("Issue disappeared after transition.");
    }

    private static (ActorType type, Guid? id, string? label) ResolveActor(Principal? p)
    {
        if (p is null) return (ActorType.System, null, null);
        if (p.PassthroughActorId is not null)
        {
            Guid? id = Guid.TryParse(p.PassthroughActorId, out var g) ? g : null;
            return (ActorType.Passthrough, id, p.PassthroughActorLabel);
        }
        var t = p.Kind == PrincipalKind.Human ? ActorType.User : ActorType.Service;
        return (t, Guid.TryParse(p.Id, out var pid) ? pid : null, p.DisplayName);
    }

    public async Task<Issue> UpdateAsync(Guid projectId, int seq, string? title, string? descriptionMd, int? priority, CancellationToken ct)
    {
        var issue = await _db.Issues
            .Include(i => i.State).Include(i => i.IssueType)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct)
            ?? throw new NotFoundException("issue_not_found", "Issue not found.");
        if (title is not null) issue.Title = title;
        if (descriptionMd is not null) issue.DescriptionMd = descriptionMd;
        if (priority is not null) issue.Priority = (Priority)priority.Value;
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _db.Activities.Add(new Activity
        {
            IssueId = issue.Id,
            ActorType = ActorType.System,
            Verb = "updated",
        });
        await _publisher.PublishAsync(WebhookEvent.IssueUpdated, projectId,
            new { issue = WebhookPayloads.From(issue), state = WebhookPayloads.From(issue.State) }, ct);
        await _db.SaveChangesAsync(ct);

        return await GetBySequenceAsync(projectId, seq, ct)
            ?? throw new InvalidOperationException("Issue vanished after update.");
    }

    public async Task<(IReadOnlyList<Issue> Items, long Total)> ListAsync(Guid projectId, int limit, string? cursor, CancellationToken ct)
    {
        var query = _db.Issues.AsNoTracking()
            .Include(i => i.State).Include(i => i.IssueType)
            .Where(i => i.ProjectId == projectId);

        if (!string.IsNullOrEmpty(cursor))
        {
            var (ts, id) = Cursor.Decode(cursor);
            query = query.Where(i => i.CreatedAt < ts || (i.CreatedAt == ts && i.Id.CompareTo(id) < 0));
        }

        var total = await _db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId).LongCountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);
        return (items, total);
    }
}
