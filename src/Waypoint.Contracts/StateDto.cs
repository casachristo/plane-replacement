namespace Waypoint.Contracts;

public sealed record StateDto(
    Guid Id,
    string Name,
    string Group,
    string Color,
    int SortOrder,
    bool IsDefault);
