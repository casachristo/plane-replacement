namespace Waypoint.Contracts;

// EpicId/CycleId let a caller file an issue already assigned to its module (epic) and
// milestone (cycle) in one POST — parity with how roadmap-structured tickets are filed
// through Cairn. Both optional; null leaves the issue unassigned on that dimension.
public sealed record CreateIssueRequest(
    string Title,
    string DescriptionMd,
    Guid? IssueTypeId = null,
    Guid? EpicId = null,
    Guid? CycleId = null,
    string? Category = null);
