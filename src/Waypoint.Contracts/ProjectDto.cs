namespace Waypoint.Contracts;

public sealed record ProjectDto(
    Guid Id,
    string Slug,
    string Name,
    string Identifier,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
