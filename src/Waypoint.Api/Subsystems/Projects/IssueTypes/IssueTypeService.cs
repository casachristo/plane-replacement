namespace Waypoint.Api.Subsystems.Projects.IssueTypes;

// Service — stateless facade over the IssueTypes feature. The Orchestrator coordinates through
// this (never the Manager) when provisioning a project.
public interface IIssueTypeService
{
    Task SeedDefaultAsync(Guid projectId, Guid defaultWorkflowId, CancellationToken ct);
}

public sealed class IssueTypeService(IIssueTypeManager manager) : IIssueTypeService
{
    public Task SeedDefaultAsync(Guid projectId, Guid defaultWorkflowId, CancellationToken ct) =>
        manager.SeedDefaultAsync(projectId, defaultWorkflowId, ct);
}
