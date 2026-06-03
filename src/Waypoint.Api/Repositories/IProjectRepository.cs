using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface IProjectRepository
{
    Task<Project> CreateAsync(string slug, string name, string identifier, CancellationToken ct);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Drops the slug-lookup cache entry for the given project. MUST be called by any
    /// future mutation that changes a project's slug, identifier, or deleted_at — without
    /// this call, GetBySlugAsync may serve stale data for up to 5 minutes.
    /// </summary>
    void InvalidateSlugCache(string slug);
}
