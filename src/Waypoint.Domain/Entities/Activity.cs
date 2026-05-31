using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class Activity
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorLabel { get; set; }
    public required string Verb { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTimeOffset At { get; set; }
}
