namespace Waypoint.Domain.Entities;

public class WorkflowTransition
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public Guid FromStateId { get; set; }
    public State FromState { get; set; } = null!;
    public Guid ToStateId { get; set; }
    public State ToState { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
