using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.ProjectCrud;

// Manager — owns Project state. The ONLY thing that persists Project rows and caches the
// slug→Project lookup. Private to the ProjectCrud feature; callers use IProjectService.
public interface IProjectManager
{
    Task<Project> AddAsync(string slug, string name, string identifier, CancellationToken ct);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct);          // AsNoTracking, cached
    Task<Project?> GetTrackedBySlugAsync(string slug, CancellationToken ct);   // tracked, for mutation
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct);
    Task SetDefaultStateAsync(Project project, Guid stateId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    void InvalidateSlugCache(string slug);
}

public sealed class ProjectManager(WaypointDbContext db, IMemoryCache cache) : IProjectManager
{
    private static readonly TimeSpan SlugCacheTtl = TimeSpan.FromMinutes(5);
    private static string Key(string slug) => $"proj:slug:{slug}";

    public async Task<Project> AddAsync(string slug, string name, string identifier, CancellationToken ct)
    {
        try
        {
            var project = new Project { Slug = slug, Name = name, Identifier = identifier };
            db.Projects.Add(project);
            await db.SaveChangesAsync(ct);
            return project;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ConflictException("project_slug_exists",
                $"A project with slug '{slug}' or identifier '{identifier}' already exists.");
        }
    }

    public async Task<Project?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        // Slug → Project is on the hot path of every issue/comment endpoint. Projects change
        // rarely, so a short TTL cache eliminates the DB roundtrip without staleness that matters.
        var key = Key(slug);
        if (cache.TryGetValue<Project>(key, out var cached)) return cached;
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (project is not null)
            cache.Set(key, project, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = SlugCacheTtl });
        return project;
    }

    public Task<Project?> GetTrackedBySlugAsync(string slug, CancellationToken ct) =>
        db.Projects.FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
        await db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public async Task SetDefaultStateAsync(Project project, Guid stateId, CancellationToken ct)
    {
        project.DefaultStateId = stateId;
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public void InvalidateSlugCache(string slug) => cache.Remove(Key(slug));
}
