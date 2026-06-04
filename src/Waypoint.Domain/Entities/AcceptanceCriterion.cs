using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class AcceptanceCriterion
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public int Position { get; set; }
    public required string Text { get; set; }
    public bool Checked { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public ActorType? CheckedByActorType { get; set; }
    public Guid? CheckedByActorId { get; set; }
    public string? CheckedByActorLabel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
