namespace Waypoint.Api.Subsystems.Projects.Workflows;

// Service — stateless facade over the Workflows feature. The Orchestrator coordinates through
// this (never the Manager) when provisioning a project.
public interface IWorkflowService
{
    Task<Guid> SeedDefaultAsync(Guid projectId, CancellationToken ct);
}

public sealed class WorkflowService(IWorkflowManager manager) : IWorkflowService
{
    public Task<Guid> SeedDefaultAsync(Guid projectId, CancellationToken ct) =>
        manager.SeedDefaultAsync(projectId, ct);
}
