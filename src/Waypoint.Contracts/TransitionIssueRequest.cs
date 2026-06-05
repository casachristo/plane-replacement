namespace Waypoint.Contracts;

public sealed record TransitionIssueRequest(
    Guid ToStateId,
    string? CommentMd = null,
    bool Force = false,
    string? BypassReason = null);
