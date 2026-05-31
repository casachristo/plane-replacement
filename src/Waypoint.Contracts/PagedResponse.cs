namespace Waypoint.Contracts;

public sealed record PagedResponse<T>(IReadOnlyList<T> Data, string? NextCursor, long TotalCount);
