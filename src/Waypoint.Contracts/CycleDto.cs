namespace Waypoint.Contracts;

// Assign (or, with null, unassign) an issue's cycle/milestone — the sprint dimension,
// parallel to AssignEpicRequest for the epic/module dimension.
public sealed record AssignCycleRequest(Guid? CycleId);

public sealed record CycleDto(
    Guid Id,
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string State);
