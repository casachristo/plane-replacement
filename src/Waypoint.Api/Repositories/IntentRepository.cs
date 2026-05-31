using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public sealed class IntentRepository : IIntentRepository
{
    private readonly WaypointDbContext _db;
    public IntentRepository(WaypointDbContext db) => _db = db;

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
        _db.IssueIntents.Add(intent);
        await _db.SaveChangesAsync(ct);
        return intent;
    }

    public async Task ReleaseAsync(Guid intentId, int? linkedIssueSeq, CancellationToken ct)
    {
        var intent = await _db.IssueIntents.FirstOrDefaultAsync(i => i.Id == intentId, ct)
            ?? throw new NotFoundException("intent_not_found", "Intent not found.");
        if (intent.ReleasedAt is not null) return;
        intent.ReleasedAt = DateTimeOffset.UtcNow;
        if (linkedIssueSeq is not null)
        {
            var issue = await _db.Issues.FirstOrDefaultAsync(
                i => i.ProjectId == intent.ProjectId && i.SequenceId == linkedIssueSeq, ct);
            if (issue is not null) intent.LinkedIssueId = issue.Id;
        }
        await _db.SaveChangesAsync(ct);
    }
}
