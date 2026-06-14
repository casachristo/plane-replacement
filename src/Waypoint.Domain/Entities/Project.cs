namespace Waypoint.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Identifier { get; set; }
    public Guid? DefaultStateId { get; set; }
    // WAY-15: links this project to a Cairn architecture catalog by name. NULL = not linked
    // = the board renders as a single-row state Kanban (unchanged behaviour).
    public string? CairnProjectName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
