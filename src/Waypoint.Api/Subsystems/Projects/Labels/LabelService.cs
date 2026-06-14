using Waypoint.Api.Subsystems.Projects.ProjectCrud;
using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.Labels;

// Service — stateless facade over the Labels feature. Resolves the project through the sibling
// ProjectService, lists labels, maps to LabelDto. Endpoints depend on this; no DbContext.
public interface ILabelService
{
    Task<IReadOnlyList<LabelDto>> ListByProjectSlugAsync(string slug, CancellationToken ct);
}

public sealed class LabelService(IProjectService projects, ILabelManager manager) : ILabelService
{
    public async Task<IReadOnlyList<LabelDto>> ListByProjectSlugAsync(string slug, CancellationToken ct)
    {
        var project = await projects.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        return (await manager.ListByProjectAsync(project.Id, ct)).Select(Map).ToList();
    }

    private static LabelDto Map(Label l) => new(l.Id, l.Name, l.Color, l.ParentLabelId);
}
