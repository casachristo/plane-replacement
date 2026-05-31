using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface ICommentRepository
{
    Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct);
    Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct);
}
