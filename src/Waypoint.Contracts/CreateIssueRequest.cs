namespace Waypoint.Contracts;

public sealed record CreateIssueRequest(string Title, string DescriptionMd, Guid? IssueTypeId = null);
