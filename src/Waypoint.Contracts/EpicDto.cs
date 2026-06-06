namespace Waypoint.Contracts;

// An Epic is Waypoint's feature-grouping ("module" in Plane terms) — a board can be grouped
// by it instead of by sprint/cycle.
public sealed record EpicDto(Guid Id, int Sequence, string Title, string Status);

public sealed record CreateEpicRequest(string Title);

// Assign (or, with null, unassign) an issue's epic/module.
public sealed record AssignEpicRequest(Guid? EpicId);
