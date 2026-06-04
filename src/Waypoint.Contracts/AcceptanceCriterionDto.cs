namespace Waypoint.Contracts;

public sealed record AcceptanceCriterionDto(
    Guid Id,
    int Position,
    string Text,
    bool Checked,
    DateTimeOffset? CheckedAt,
    string? CheckedByActorType,
    Guid? CheckedByActorId,
    string? CheckedByActorLabel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
