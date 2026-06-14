using Waypoint.Api.Auth;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface IIssueRepository
{
    Task<Issue> CreateAsync(Guid projectId, string title, string descriptionMd, Guid? issueTypeId, Guid? epicId, Guid? cycleId, Waypoint.Domain.Enums.TicketCategory category, CancellationToken ct);
    Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct);
    Task<Issue> TransitionAsync(Guid projectId, int seq, Guid toStateId, bool force, string? bypassReason, Principal? actor, CancellationToken ct);
    Task<Issue> UpdateAsync(Guid projectId, int seq, string? title, string? descriptionMd, int? priority, CancellationToken ct);
    Task<(IReadOnlyList<Issue> Items, long Total)> ListAsync(Guid projectId, int limit, string? cursor, Waypoint.Domain.Enums.TicketCategory? category, CancellationToken ct);
    Task<int> NextSequenceAsync(Guid projectId, CancellationToken ct);
}
