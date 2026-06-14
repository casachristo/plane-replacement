namespace Waypoint.Contracts;

// WAY-15: the module-swimlane shape for a project board. CairnLinked=false (no Cairn
// project linked) => Modules is empty and the UI falls back to the single-row state Kanban.
public sealed record SwimlanesDto(bool CairnLinked, IReadOnlyList<string> Modules);

// Sets (or clears, with null) the Cairn architecture catalog this project draws module rows from.
public sealed record SetCairnLinkRequest(string? CairnProjectName);
