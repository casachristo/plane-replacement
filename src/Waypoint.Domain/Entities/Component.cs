namespace Waypoint.Domain.Entities;

public class Component
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class IssueComponent
{
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public Guid ComponentId { get; set; }
    public Component Component { get; set; } = null!;
}
