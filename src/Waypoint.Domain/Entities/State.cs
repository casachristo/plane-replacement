using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class State
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public StateGroup Group { get; set; }
    public required string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
