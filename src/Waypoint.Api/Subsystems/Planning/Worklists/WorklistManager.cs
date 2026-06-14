using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Webhooks;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Planning.Worklists;

// Manager — owns the per-project singleton batch Worklist (WAY-17/18). Drives the
// start → advance/skip → drain state machine and fires worklist.current_advanced on every
// pointer change in the SAME SaveChanges as the change. The advance event is transactionally
// part of the state transition, so this leaf feature's effect lives in the Manager (there is no
// Planning Orchestrator). The only thing that persists Worklist; private to the feature.
public interface IWorklistManager
{
    Task SeedAsync(Guid projectId, CancellationToken ct);
    Task<(Worklist worklist, Issue? current)> GetAsync(Guid projectId, CancellationToken ct);
    Task<(Worklist worklist, Issue? current)> StartAsync(Guid projectId, CancellationToken ct);
    Task<(Worklist worklist, Issue? current)> AdvanceAsync(Guid projectId, CancellationToken ct);
    Task<(Worklist worklist, Issue? current)> SkipAsync(Guid projectId, string reason, CancellationToken ct);
    Task<Worklist> StopAsync(Guid projectId, CancellationToken ct);
}

public sealed class WorklistManager(WaypointDbContext db, IWebhookPublisher publisher) : IWorklistManager
{
    public async Task SeedAsync(Guid projectId, CancellationToken ct)
    {
        db.Set<Worklist>().Add(new Worklist { ProjectId = projectId, State = WorklistState.Inactive });
        await db.SaveChangesAsync(ct);
    }

    public async Task<(Worklist, Issue?)> GetAsync(Guid projectId, CancellationToken ct)
    {
        var wl = await LoadAsync(projectId, ct);
        return (wl, await LoadIssueAsync(wl.CurrentId, ct));
    }

    public async Task<(Worklist, Issue?)> StartAsync(Guid projectId, CancellationToken ct)
    {
        var wl = await LoadAsync(projectId, ct);
        if (wl.State == WorklistState.Active)
            throw new ConflictException("worklist_active",
                "Worklist is already active; stop it before starting a new run.");

        // Rebuild the queue from the project's workable tickets: Todo (Unstarted) + Backlog
        // (Icebox) groups, highest priority first, then oldest sequence first.
        wl.OrderedIds = await db.Issues
            .Where(i => i.ProjectId == projectId && i.DeletedAt == null
                        && (i.State.Group == StateGroup.Unstarted || i.State.Group == StateGroup.Backlog))
            .OrderByDescending(i => i.Priority).ThenBy(i => i.SequenceId)
            .Select(i => i.Id)
            .ToListAsync(ct);
        wl.CurrentIdx = 0;
        wl.DoneCount = 0;
        wl.Skipped = new();
        wl.StartedAt = DateTimeOffset.UtcNow;
        wl.CompletedAt = null;
        wl.State = WorklistState.Active;
        if (wl.OrderedIds.Count == 0) DrainIfDone(wl);   // empty start drains immediately

        var current = await LoadIssueAsync(wl.CurrentId, ct);
        await PublishAdvancedAsync(projectId, wl, previous: null, current,
            trigger: wl.State == WorklistState.Active ? "advance" : "drained", reason: null, ct);
        await SaveAsync(wl, ct);
        return (wl, current);
    }

    public async Task<(Worklist, Issue?)> AdvanceAsync(Guid projectId, CancellationToken ct)
    {
        var wl = await LoadAsync(projectId, ct);
        // Idempotent: advancing a drained/inactive worklist is a harmless no-op.
        if (wl.State != WorklistState.Active) return (wl, null);

        var previous = await LoadIssueAsync(wl.CurrentId, ct);
        wl.DoneCount++;
        wl.CurrentIdx++;
        DrainIfDone(wl);

        var current = await LoadIssueAsync(wl.CurrentId, ct);
        await PublishAdvancedAsync(projectId, wl, previous, current,
            trigger: wl.State == WorklistState.Active ? "advance" : "drained", reason: null, ct);
        await SaveAsync(wl, ct);
        return (wl, current);
    }

    public async Task<(Worklist, Issue?)> SkipAsync(Guid projectId, string reason, CancellationToken ct)
    {
        var wl = await LoadAsync(projectId, ct);
        if (wl.State != WorklistState.Active) return (wl, null);

        var previous = await LoadIssueAsync(wl.CurrentId, ct);
        if (previous is not null)
        {
            wl.Skipped.Add(new WorklistSkip { IssueId = previous.Id, Reason = reason });
            db.Comments.Add(new Comment
            {
                IssueId = previous.Id,
                BodyMd = $"Skipped during batch run: {reason}",
            });
        }
        wl.CurrentIdx++;
        DrainIfDone(wl);

        var current = await LoadIssueAsync(wl.CurrentId, ct);
        await PublishAdvancedAsync(projectId, wl, previous, current,
            trigger: wl.State == WorklistState.Active ? "skip" : "drained", reason, ct);
        await SaveAsync(wl, ct);
        return (wl, current);
    }

    public async Task<Worklist> StopAsync(Guid projectId, CancellationToken ct)
    {
        var wl = await LoadAsync(projectId, ct);
        wl.State = WorklistState.Inactive;   // keep data so a later start rebuilds / resume re-activates
        await SaveAsync(wl, ct);
        return wl;
    }

    private async Task<Worklist> LoadAsync(Guid projectId, CancellationToken ct) =>
        await db.Set<Worklist>().FirstOrDefaultAsync(w => w.ProjectId == projectId, ct)
            ?? throw new NotFoundException("worklist_not_found", "Project has no worklist.");

    private async Task<Issue?> LoadIssueAsync(Guid? issueId, CancellationToken ct)
    {
        if (issueId is null) return null;
        return await db.Issues.Include(i => i.State).Include(i => i.IssueType).Include(i => i.Epic)
            .FirstOrDefaultAsync(i => i.Id == issueId.Value, ct);
    }

    private static void DrainIfDone(Worklist wl)
    {
        if (wl.CurrentIdx >= wl.OrderedIds.Count)
        {
            wl.State = WorklistState.Inactive;
            wl.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task PublishAdvancedAsync(
        Guid projectId, Worklist wl, Issue? previous, Issue? current, string trigger, string? reason, CancellationToken ct)
    {
        var project = await db.Projects.FirstAsync(p => p.Id == projectId, ct);
        await publisher.PublishAsync(WebhookEvent.WorklistCurrentAdvanced, projectId,
            WebhookPayloads.WorklistAdvanced(project, previous, current, wl, trigger, reason), ct);
    }

    private async Task SaveAsync(Worklist wl, CancellationToken ct)
    {
        wl.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
