namespace Waypoint.Domain.Entities;

public class IssueIntent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string ModulePath { get; set; }
    public required string IntentText { get; set; }
    public Guid DeclaredByTokenId { get; set; }
    public ApiToken DeclaredByToken { get; set; } = null!;
    public DateTimeOffset LockAcquiredAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public Guid? LinkedIssueId { get; set; }
    public Issue? LinkedIssue { get; set; }
}
