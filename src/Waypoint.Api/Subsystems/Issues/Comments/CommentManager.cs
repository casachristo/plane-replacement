using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Issues.Comments;

// Manager — owns Comment state. The ONLY thing that persists comments (and the paired
// "commented" activity). Private to the Comments feature; callers use ICommentService.
public interface ICommentManager
{
    Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct);
    Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct);
}

public sealed class CommentManager(WaypointDbContext db) : ICommentManager
{
    public async Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct)
    {
        var comment = new Comment { IssueId = issueId, BodyMd = bodyMd, AuthorUserId = authorUserId };
        db.Comments.Add(comment);
        db.Activities.Add(new Activity
        {
            IssueId = issueId,
            ActorType = authorUserId is null ? ActorType.System : ActorType.User,
            ActorId = authorUserId,
            Verb = "commented",
        });
        await db.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct) =>
        await db.Comments.AsNoTracking()
            .Where(c => c.IssueId == issueId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
}
