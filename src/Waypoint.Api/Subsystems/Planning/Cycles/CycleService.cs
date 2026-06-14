using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Planning.Cycles;

// Service — stateless facade over the Cycles feature. Lists a project's cycles (mapped to DTO)
// and exposes the membership check the Issues subsystem uses when assigning an issue to a cycle.
public interface ICycleService
{
    Task<IReadOnlyList<CycleDto>> ListAsync(string slug, CancellationToken ct);
    Task<bool> ExistsInProjectAsync(Guid cycleId, Guid projectId, CancellationToken ct);
}

public sealed class CycleService(IProjectService projects, ICycleManager manager) : ICycleService
{
    public async Task<IReadOnlyList<CycleDto>> ListAsync(string slug, CancellationToken ct)
    {
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return (await manager.ListByProjectAsync(project.Id, ct)).Select(Map).ToList();
    }

    public Task<bool> ExistsInProjectAsync(Guid cycleId, Guid projectId, CancellationToken ct) =>
        manager.ExistsInProjectAsync(cycleId, projectId, ct);

    private static CycleDto Map(Cycle c) => new(c.Id, c.Name, c.StartDate, c.EndDate, c.State);
}
