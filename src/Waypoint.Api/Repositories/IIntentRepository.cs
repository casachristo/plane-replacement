using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface IIntentRepository
{
    Task<IssueIntent> FileAsync(Guid projectId, string modulePath, string intentText, Guid declaredByTokenId, CancellationToken ct);
    Task ReleaseAsync(Guid intentId, int? linkedIssueSeq, CancellationToken ct);
}
