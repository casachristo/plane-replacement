namespace Waypoint.Contracts;

/// <summary>WAY-17: snapshot of a project's batch worklist for the internal GET endpoint.</summary>
public sealed record WorklistStatusDto(
    string State,
    IssueDto? Current,
    int RemainingCount,
    int DoneCount,
    int SkippedCount,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

/// <summary>WAY-17: body for POST /worklist/skip — reason is required and non-empty.</summary>
public sealed record SkipWorklistRequest(string Reason);

/// <summary>WAY-17: end-of-run summary returned by POST /worklist/stop.</summary>
public sealed record WorklistStopSummary(int DoneCount, int SkippedCount, int RemainingCount);
