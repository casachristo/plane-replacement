namespace Waypoint.Contracts;

// Assign (or, with null, unassign) an issue's cycle/milestone — the sprint dimension,
// parallel to AssignEpicRequest for the epic/module dimension.
public sealed record AssignCycleRequest(Guid? CycleId);
