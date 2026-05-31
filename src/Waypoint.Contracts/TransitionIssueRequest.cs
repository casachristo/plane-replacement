namespace Waypoint.Contracts;

public sealed record TransitionIssueRequest(Guid ToStateId, string? CommentMd = null);
