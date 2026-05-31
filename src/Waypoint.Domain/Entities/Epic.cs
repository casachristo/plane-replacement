namespace Waypoint.Domain.Entities;

public class Epic
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int SequenceId { get; set; }
    public required string Title { get; set; }
    public string DescriptionMd { get; set; } = string.Empty;
    public string Status { get; set; } = "planned";       // planned / in-flight / done
    public Guid? TargetCycleId { get; set; }
    public Cycle? TargetCycle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
