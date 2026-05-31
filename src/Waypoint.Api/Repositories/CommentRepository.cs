using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Repositories;

public sealed class CommentRepository : ICommentRepository
{
    private readonly WaypointDbContext _db;
    public CommentRepository(WaypointDbContext db) => _db = db;

    public async Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct)
    {
        var comment = new Comment { IssueId = issueId, BodyMd = bodyMd, AuthorUserId = authorUserId };
        _db.Comments.Add(comment);
        _db.Activities.Add(new Activity
        {
            IssueId = issueId,
            ActorType = authorUserId is null ? ActorType.System : ActorType.User,
            ActorId = authorUserId,
            Verb = "commented",
        });
        await _db.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct) =>
        await _db.Comments.AsNoTracking()
            .Where(c => c.IssueId == issueId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
}
