using Waypoint.Contracts;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.ProjectCrud;

// Service — the stateless interface to the ProjectCrud feature. Validates/maps and delegates
// state to the Manager. Other layers depend on this, never on the Manager. Project provisioning
// (which spans the States/Workflow/IssueType features) lives in the subsystem Orchestrator, not
// here — this Service only owns single-feature project operations.
public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct);
    Task<ProjectDto?> GetAsync(string slug, CancellationToken ct);
    Task<ProjectDto> SetCairnLinkAsync(string slug, SetCairnLinkRequest req, CancellationToken ct);

    // Cross-subsystem slug → project resolution. Other subsystems depend on this facade to
    // resolve a project (and read its Id) without reaching into the Manager or the DbContext.
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct);

    // Provisioning primitives used by the ProjectsOrchestrator (intra-subsystem coordination).
    Task<Project> AddAsync(string slug, string name, string identifier, CancellationToken ct);
    Task SetDefaultStateAsync(Project project, Guid stateId, CancellationToken ct);
}

public sealed class ProjectService(IProjectManager manager) : IProjectService
{
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct) =>
        (await manager.ListAsync(ct)).Select(ProjectMapper.ToDto).ToList();

    public async Task<ProjectDto?> GetAsync(string slug, CancellationToken ct)
    {
        var p = await manager.GetBySlugAsync(slug, ct);
        return p is null ? null : ProjectMapper.ToDto(p);
    }

    public async Task<ProjectDto> SetCairnLinkAsync(string slug, SetCairnLinkRequest req, CancellationToken ct)
    {
        var p = await manager.GetTrackedBySlugAsync(slug, ct)
            ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
        p.CairnProjectName = string.IsNullOrWhiteSpace(req.CairnProjectName) ? null : req.CairnProjectName.Trim();
        await manager.SaveAsync(ct);
        // The slug cache holds a stale snapshot whose CairnProjectName is now wrong; drop it so
        // GET /swimlanes reflects the new link immediately instead of after the 5-minute TTL.
        manager.InvalidateSlugCache(slug);
        return ProjectMapper.ToDto(p);
    }

    public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct) => manager.GetBySlugAsync(slug, ct);

    public Task<Project> AddAsync(string slug, string name, string identifier, CancellationToken ct) =>
        manager.AddAsync(slug, name, identifier, ct);

    public Task SetDefaultStateAsync(Project project, Guid stateId, CancellationToken ct) =>
        manager.SetDefaultStateAsync(project, stateId, ct);
}
