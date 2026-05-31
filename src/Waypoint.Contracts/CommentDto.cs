namespace Waypoint.Contracts;

public sealed record CommentDto(
    Guid Id,
    Guid IssueId,
    string BodyMd,
    Guid? AuthorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
