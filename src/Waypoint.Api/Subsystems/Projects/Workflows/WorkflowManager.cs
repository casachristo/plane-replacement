using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.Workflows;

// Manager — owns Workflow rows. Seeds a project's default workflow. The only thing that
// persists Workflow; private to the Workflows feature.
public interface IWorkflowManager
{
    // Seeds the project's "Default" workflow; returns its Id so the default issue type can
    // point at it.
    Task<Guid> SeedDefaultAsync(Guid projectId, CancellationToken ct);
}

public sealed class WorkflowManager(WaypointDbContext db) : IWorkflowManager
{
    public async Task<Guid> SeedDefaultAsync(Guid projectId, CancellationToken ct)
    {
        var workflow = new Workflow { ProjectId = projectId, Name = "Default" };
        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(ct);
        return workflow.Id;
    }
}
