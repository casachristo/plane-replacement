using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Planning.Epics;

// Service — stateless facade over the Epics feature. Resolves the project through the Projects
// subsystem facade, validates input, maps to EpicDto. Endpoints depend on this; no DbContext.
public interface IEpicService
{
    Task<IReadOnlyList<EpicDto>> ListAsync(string slug, CancellationToken ct);
    Task<EpicDto> CreateAsync(string slug, CreateEpicRequest req, CancellationToken ct);
}

public sealed class EpicService(IProjectService projects, IEpicManager manager) : IEpicService
{
    public async Task<IReadOnlyList<EpicDto>> ListAsync(string slug, CancellationToken ct)
    {
        var project = await Resolve(slug, ct);
        return (await manager.ListByProjectAsync(project.Id, ct)).Select(Map).ToList();
    }

    public async Task<EpicDto> CreateAsync(string slug, CreateEpicRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new ValidationException("title_required", "An epic title is required.");
        var project = await Resolve(slug, ct);
        return Map(await manager.CreateAsync(project.Id, req.Title.Trim(), ct));
    }

    private async Task<Project> Resolve(string slug, CancellationToken ct) =>
        await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");

    private static EpicDto Map(Epic e) => new(e.Id, e.SequenceId, e.Title, e.Status);
}
