namespace Waypoint.Contracts;

public sealed record IssueDto(
    Guid Id,
    int Sequence,
    string Title,
    string DescriptionMd,
    Guid StateId,
    string StateName,
    Guid IssueTypeId,
    string IssueTypeName,
    int Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
