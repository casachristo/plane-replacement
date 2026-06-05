using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Pagination;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Repositories;

public sealed class IssueRepository : IIssueRepository
{
    private readonly WaypointDbContext _db;
    public IssueRepository(WaypointDbContext db) => _db = db;

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

    public async Task<Issue> CreateAsync(Guid projectId, string title, string descriptionMd, Guid? issueTypeId, CancellationToken ct)
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

        var seq = await NextSequenceAsync(projectId, ct);
        var issue = new Issue
        {
            ProjectId = projectId,
            SequenceId = seq,
            Title = title,
            DescriptionMd = descriptionMd,
            StateId = project.DefaultStateId.Value,
            IssueTypeId = typeId,
        };
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync(ct);

        _db.Activities.Add(new Activity
        {
            IssueId = issue.Id,
            ActorType = ActorType.System,
            Verb = "created",
        });
        await _db.SaveChangesAsync(ct);

        return issue;
    }

    public Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct) =>
        _db.Issues.AsNoTracking()
            .Include(i => i.State)
            .Include(i => i.IssueType)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct);

    public async Task<Issue> TransitionAsync(Guid projectId, int seq, Guid toStateId, bool force, CancellationToken ct)
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

        // WAY-4: pre-fact gate — closing an issue (group=Completed) requires every AC item
        // checked. Bypass with force=true (WAY-9 will audit the bypass separately).
        if (!force && newState.Group == StateGroup.Completed)
        {
            var unchecked_ = await _db.Set<AcceptanceCriterion>().AsNoTracking()
                .Where(a => a.IssueId == issue.Id && !a.Checked)
                .OrderBy(a => a.Position)
                .Select(a => new { id = a.Id, position = a.Position, text = a.Text })
                .ToListAsync(ct);
            if (unchecked_.Count > 0)
            {
                throw new PreconditionFailedException(
                    "acceptance_criteria_unchecked",
                    $"Cannot transition to '{newState.Name}' — {unchecked_.Count} acceptance criterion(s) are still unchecked.",
                    new Dictionary<string, object> { ["unchecked"] = unchecked_ });
            }
        }

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
        await _db.SaveChangesAsync(ct);

        return await GetBySequenceAsync(projectId, seq, ct)
            ?? throw new InvalidOperationException("Issue disappeared after transition.");
    }

    public async Task<Issue> UpdateAsync(Guid projectId, int seq, string? title, string? descriptionMd, int? priority, CancellationToken ct)
    {
        var issue = await _db.Issues.FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct)
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
