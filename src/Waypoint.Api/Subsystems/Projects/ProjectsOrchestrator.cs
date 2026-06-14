using Waypoint.Api.Repositories;
using Waypoint.Api.Subsystems.Projects.IssueTypes;
using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Api.Subsystems.Projects.States;
using Waypoint.Api.Subsystems.Projects.Workflows;
using Waypoint.Contracts;

namespace Waypoint.Api.Subsystems.Projects;

// Orchestrator — coordinates the Projects subsystem's child features to provision a project.
// Creating a project is the canonical cross-feature flow: it seeds the default states, points
// the project at its landing state, seeds the default workflow + issue type, and creates the
// dormant batch worklist. Calls child Services only (never their Managers); holds no state.
public interface IProjectsOrchestrator
{
    Task<ProjectDto> ProvisionAsync(CreateProjectRequest req, CancellationToken ct);
}

public sealed class ProjectsOrchestrator(
    IProjectService projects,
    IStateService states,
    IWorkflowService workflows,
    IIssueTypeService issueTypes,
    IWorklistRepository worklists) : IProjectsOrchestrator
{
    public async Task<ProjectDto> ProvisionAsync(CreateProjectRequest req, CancellationToken ct)
    {
        var project = await projects.AddAsync(req.Slug, req.Name, req.Identifier, ct);

        var defaultStateId = await states.SeedDefaultsAsync(project.Id, ct);
        await projects.SetDefaultStateAsync(project, defaultStateId, ct);

        var workflowId = await workflows.SeedDefaultAsync(project.Id, ct);
        await issueTypes.SeedDefaultAsync(project.Id, workflowId, ct);

        // WAY-17: every project gets its singleton batch Worklist, dormant until Cairn starts it.
        // (Worklist is a Planning-subsystem concern; seeded here at provisioning via its facade.)
        await worklists.SeedAsync(project.Id, ct);

        return ProjectMapper.ToDto(project);
    }
}
