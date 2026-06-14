using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Pagination;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Issues.IssueCrud;

// Manager — owns Issue state. The only thing that persists issues and their co-located audit
// activity. Holds no orchestration (no webhooks, no cross-feature gates); those live in the
// Service / Orchestrator. Private to the Issues subsystem.
public interface IIssueManager
{
    Task<int> NextSequenceAsync(Guid projectId, CancellationToken ct);
    Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct);
    Task<Issue?> GetTrackedAsync(Guid projectId, int seq, CancellationToken ct);
    Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct);
    Task<Guid?> ResolveDefaultIssueTypeAsync(Guid projectId, CancellationToken ct);
    Task<bool> EpicExistsAsync(Guid projectId, Guid epicId, CancellationToken ct);
    Task<bool> CycleExistsAsync(Guid projectId, Guid cycleId, CancellationToken ct);
    Task<State?> GetStateAsync(Guid stateId, CancellationToken ct);
    Task<IReadOnlyList<(Guid From, Guid To)>> GetWorkflowTransitionsAsync(Guid workflowId, CancellationToken ct);
    Task<IReadOnlyList<UncheckedCriterion>> GetUncheckedCriteriaAsync(Guid issueId, CancellationToken ct);
    Task PersistNewAsync(Issue issue, CancellationToken ct);
    Task PersistFieldUpdateAsync(Issue tracked, CancellationToken ct);
    Task PersistTransitionAsync(Issue tracked, Guid beforeStateId, Guid toStateId, GateOverrideEvent? overrideEvent, CancellationToken ct);
    // Flush staged state (e.g. webhook deliveries staged by IWebhookPublisher on the shared
    // DbContext) after the Service/Orchestrator has published its cross-cutting events.
    Task SaveAsync(CancellationToken ct);
    Task<(IReadOnlyList<Issue> Items, long Total)> ListAsync(Guid projectId, int limit, string? cursor, TicketCategory? category, CancellationToken ct);
}

public sealed record UncheckedCriterion(Guid id, int position, string text);

public sealed class IssueManager(WaypointDbContext db) : IIssueManager
{
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
        await db.Database.ExecuteSqlRawAsync(ensure, ct);

        var conn = db.Database.GetDbConnection();
        var opened = false;
        if (conn.State != System.Data.ConnectionState.Open) { await conn.OpenAsync(ct); opened = true; }
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT nextval('{seqName}')";
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32((long)result!);
        }
        finally { if (opened) await conn.CloseAsync(); }
    }

    public Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct) =>
        db.Issues.AsNoTracking()
            .Include(i => i.State).Include(i => i.IssueType).Include(i => i.Epic).Include(i => i.Cycle)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct);

    public Task<Issue?> GetTrackedAsync(Guid projectId, int seq, CancellationToken ct) =>
        db.Issues.Include(i => i.State).Include(i => i.IssueType)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct);

    public async Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.Projects.FindAsync([projectId], ct);

    public Task<Guid?> ResolveDefaultIssueTypeAsync(Guid projectId, CancellationToken ct) =>
        db.IssueTypes.Where(t => t.ProjectId == projectId && t.IsDefault).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

    public Task<bool> EpicExistsAsync(Guid projectId, Guid epicId, CancellationToken ct) =>
        db.Set<Epic>().AnyAsync(e => e.Id == epicId && e.ProjectId == projectId, ct);

    public Task<bool> CycleExistsAsync(Guid projectId, Guid cycleId, CancellationToken ct) =>
        db.Set<Cycle>().AnyAsync(c => c.Id == cycleId && c.ProjectId == projectId, ct);

    public async Task<State?> GetStateAsync(Guid stateId, CancellationToken ct) =>
        await db.States.FindAsync([stateId], ct);

    public async Task<IReadOnlyList<(Guid From, Guid To)>> GetWorkflowTransitionsAsync(Guid workflowId, CancellationToken ct) =>
        (await db.WorkflowTransitions.Where(t => t.WorkflowId == workflowId)
            .Select(t => new { t.FromStateId, t.ToStateId }).ToListAsync(ct))
            .Select(t => (t.FromStateId, t.ToStateId)).ToList();

    public async Task<IReadOnlyList<UncheckedCriterion>> GetUncheckedCriteriaAsync(Guid issueId, CancellationToken ct) =>
        await db.Set<AcceptanceCriterion>().AsNoTracking()
            .Where(a => a.IssueId == issueId && !a.Checked).OrderBy(a => a.Position)
            .Select(a => new UncheckedCriterion(a.Id, a.Position, a.Text)).ToListAsync(ct);

    public async Task PersistNewAsync(Issue issue, CancellationToken ct)
    {
        db.Issues.Add(issue);
        await db.SaveChangesAsync(ct);
        db.Activities.Add(new Activity { IssueId = issue.Id, ActorType = ActorType.System, Verb = "created" });
        await db.SaveChangesAsync(ct);
    }

    public async Task PersistFieldUpdateAsync(Issue tracked, CancellationToken ct)
    {
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        db.Activities.Add(new Activity { IssueId = tracked.Id, ActorType = ActorType.System, Verb = "updated" });
        await db.SaveChangesAsync(ct);
    }

    public async Task PersistTransitionAsync(Issue tracked, Guid beforeStateId, Guid toStateId, GateOverrideEvent? overrideEvent, CancellationToken ct)
    {
        if (overrideEvent is not null) db.Set<GateOverrideEvent>().Add(overrideEvent);
        tracked.StateId = toStateId;
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        db.Activities.Add(new Activity
        {
            IssueId = tracked.Id, ActorType = ActorType.System, Verb = "transitioned",
            BeforeJson = $$"""{"state_id":"{{beforeStateId}}"}""",
            AfterJson = $$"""{"state_id":"{{toStateId}}"}""",
        });
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<(IReadOnlyList<Issue> Items, long Total)> ListAsync(Guid projectId, int limit, string? cursor, TicketCategory? category, CancellationToken ct)
    {
        var query = db.Issues.AsNoTracking().Include(i => i.State).Include(i => i.IssueType)
            .Where(i => i.ProjectId == projectId);
        if (!string.IsNullOrEmpty(cursor))
        {
            var (ts, id) = Cursor.Decode(cursor);
            query = query.Where(i => i.CreatedAt < ts || (i.CreatedAt == ts && i.Id.CompareTo(id) < 0));
        }
        if (category is { } cat) query = query.Where(i => i.Category == cat);
        var total = await db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId).LongCountAsync(ct);
        var items = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Take(Math.Clamp(limit, 1, IssueService.MaxPageSize)).ToListAsync(ct);
        return (items, total);
    }
}
