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
    Guid? EpicId,
    string? EpicTitle,
    Guid? CycleId,
    string? CycleName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    // Populated by GET /api/v1/projects/{slug}/issues/{seq}; empty array on POST/PATCH/transition
    // (no separate fetch — keeps those handlers' hot path lean). Use the dedicated
    // /acceptance-criteria endpoints for CRUD on AC items.
    public IReadOnlyList<AcceptanceCriterionDto> AcceptanceCriteria { get; init; } = [];
}
