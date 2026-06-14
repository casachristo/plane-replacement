using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.States;

// Service — stateless facade over the States feature. Resolves the project through the sibling
// ProjectService (never its Manager), lists states, maps to StateDto. Endpoints depend on this
// and never touch the DbContext. Also exposes the provisioning seed for the Orchestrator.
public interface IStateService
{
    Task<IReadOnlyList<StateDto>> ListByProjectSlugAsync(string slug, CancellationToken ct);
    Task<Guid> SeedDefaultsAsync(Guid projectId, CancellationToken ct);
}

public sealed class StateService(IProjectService projects, IStateManager manager) : IStateService
{
    public async Task<IReadOnlyList<StateDto>> ListByProjectSlugAsync(string slug, CancellationToken ct)
    {
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return (await manager.ListByProjectAsync(project.Id, ct)).Select(Map).ToList();
    }

    public Task<Guid> SeedDefaultsAsync(Guid projectId, CancellationToken ct) =>
        manager.SeedDefaultsAsync(projectId, ct);

    private static StateDto Map(State s) =>
        new(s.Id, s.Name, s.Group.ToString(), s.Color, s.SortOrder, s.IsDefault);
}
