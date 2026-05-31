using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class Issue
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int SequenceId { get; set; }
    public required string Title { get; set; }
    public string DescriptionMd { get; set; } = string.Empty;
    public Guid StateId { get; set; }
    public State State { get; set; } = null!;
    public Priority Priority { get; set; } = Priority.None;
    public Guid IssueTypeId { get; set; }
    public IssueType IssueType { get; set; } = null!;
    public Guid? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public Guid? EpicId { get; set; }
    public Epic? Epic { get; set; }
    public Guid? CycleId { get; set; }
    public Cycle? Cycle { get; set; }
    public Guid[] AssigneeIds { get; set; } = [];
    public DateTimeOffset? DueDate { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
