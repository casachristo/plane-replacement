namespace Waypoint.Contracts;

public sealed record UpdateIssueRequest(string? Title = null, string? DescriptionMd = null, int? Priority = null);
