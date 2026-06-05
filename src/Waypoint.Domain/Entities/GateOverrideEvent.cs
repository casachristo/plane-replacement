using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

/// <summary>
/// WAY-9: durable audit row for every time a server-side gate is bypassed with
/// force=true. The point is forensic visibility — gates SHOULD be force-able by
/// humans (or agents) with reason, but every override stays reviewable after the
/// fact. Currently emitted by WAY-4's AC-on-Completed gate; the same shape will
/// cover future gates by tagging via GateName.
/// </summary>
public class GateOverrideEvent
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public required string GateName { get; set; }
    public required string Reason { get; set; }
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorLabel { get; set; }
    public DateTimeOffset At { get; set; }
}
