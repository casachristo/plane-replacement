namespace Waypoint.Domain.Entities;

public class Label
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public required string Color { get; set; }
    public Guid? ParentLabelId { get; set; }
    public Label? ParentLabel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class IssueLabel
{
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public Guid LabelId { get; set; }
    public Label Label { get; set; } = null!;
}
