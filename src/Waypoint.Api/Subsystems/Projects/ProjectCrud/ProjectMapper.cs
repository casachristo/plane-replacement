using Waypoint.Contracts;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.ProjectCrud;

// Shared Project → DTO mapping for the ProjectCrud feature. Used by the Service and the
// subsystem Orchestrator so the wire shape is defined in exactly one place.
internal static class ProjectMapper
{
    public static ProjectDto ToDto(Project p) =>
        new(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt, p.CairnProjectName);
}
