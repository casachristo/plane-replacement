using Waypoint.Api.Auth;
using Waypoint.Contracts;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Issues.Comments;

// Service — the stateless interface to the Comments feature. Resolves the author from the
// principal, delegates state changes to the Manager, and maps to the wire DTO. No state.
public interface ICommentService
{
    Task<CommentDto> AddAsync(Guid issueId, string bodyMd, Principal author, CancellationToken ct);
    Task<IReadOnlyList<CommentDto>> ListAsync(Guid issueId, CancellationToken ct);
}

public sealed class CommentService(ICommentManager manager) : ICommentService
{
    public async Task<CommentDto> AddAsync(Guid issueId, string bodyMd, Principal author, CancellationToken ct)
    {
        var authorId = author.Kind == PrincipalKind.Human && Guid.TryParse(author.Id, out var uid) ? uid : (Guid?)null;
        return Map(await manager.CreateAsync(issueId, bodyMd, authorId, ct));
    }

    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid issueId, CancellationToken ct) =>
        (await manager.ListByIssueAsync(issueId, ct)).Select(Map).ToList();

    private static CommentDto Map(Comment c) =>
        new(c.Id, c.IssueId, c.BodyMd, c.AuthorUserId, c.CreatedAt, c.UpdatedAt);
}
