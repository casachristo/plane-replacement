using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface IProjectRepository
{
    Task<Project> CreateAsync(string slug, string name, string identifier, CancellationToken ct);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct);
}
