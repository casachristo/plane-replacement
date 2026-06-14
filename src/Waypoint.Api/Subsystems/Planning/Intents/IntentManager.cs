using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Planning.Intents;

// Manager — owns IssueIntent state (the per-module work-intent lock Cairn's dispatcher files
// before an agent starts, released when the work lands). The only thing that persists
// IssueIntent; private to the Intents feature.
public interface IIntentManager
{
    Task<IssueIntent> FileAsync(Guid projectId, string modulePath, string intentText, Guid declaredByTokenId, CancellationToken ct);
    Task ReleaseAsync(Guid intentId, int? linkedIssueSeq, CancellationToken ct);
}

public sealed class IntentManager(WaypointDbContext db) : IIntentManager
{
    public async Task<IssueIntent> FileAsync(Guid projectId, string modulePath, string intentText, Guid declaredByTokenId, CancellationToken ct)
    {
        var intent = new IssueIntent
        {
            ProjectId = projectId,
            ModulePath = modulePath,
            IntentText = intentText,
            DeclaredByTokenId = declaredByTokenId,
            LockAcquiredAt = DateTimeOffset.UtcNow,
        };
        db.IssueIntents.Add(intent);
        await db.SaveChangesAsync(ct);
        return intent;
    }

    public async Task ReleaseAsync(Guid intentId, int? linkedIssueSeq, CancellationToken ct)
    {
        var intent = await db.IssueIntents.FirstOrDefaultAsync(i => i.Id == intentId, ct)
            ?? throw new NotFoundException("intent_not_found", "Intent not found.");
        if (intent.ReleasedAt is not null) return;
        intent.ReleasedAt = DateTimeOffset.UtcNow;
        if (linkedIssueSeq is not null)
        {
            var issue = await db.Issues.FirstOrDefaultAsync(
                i => i.ProjectId == intent.ProjectId && i.SequenceId == linkedIssueSeq, ct);
            if (issue is not null) intent.LinkedIssueId = issue.Id;
        }
        await db.SaveChangesAsync(ct);
    }
}
