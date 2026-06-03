using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly WaypointDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan SlugCacheTtl = TimeSpan.FromMinutes(5);
    public ProjectRepository(WaypointDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

    public async Task<Project> CreateAsync(string slug, string name, string identifier, CancellationToken ct)
    {
        try
        {
            var project = new Project { Slug = slug, Name = name, Identifier = identifier };
            _db.Projects.Add(project);
            await _db.SaveChangesAsync(ct);

            var defaultState = new State
            {
                ProjectId = project.Id, Name = "Backlog", Group = StateGroup.Backlog,
                Color = "#94a3b8", SortOrder = 0, IsDefault = true,
            };
            _db.States.Add(defaultState);
            await _db.SaveChangesAsync(ct);

            project.DefaultStateId = defaultState.Id;
            await _db.SaveChangesAsync(ct);

            var workflow = new Workflow { ProjectId = project.Id, Name = "Default" };
            _db.Workflows.Add(workflow);
            await _db.SaveChangesAsync(ct);

            var issueType = new IssueType
            {
                ProjectId = project.Id, Name = "Task", IsDefault = true, DefaultWorkflowId = workflow.Id,
            };
            _db.IssueTypes.Add(issueType);
            await _db.SaveChangesAsync(ct);

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
        // Slug → Project lookup is on the hot path of every issue/comment endpoint. Projects
        // change rarely (created once, almost never renamed), so a short TTL cache eliminates
        // the DB roundtrip without staleness risk that matters.
        var key = $"proj:slug:{slug}";
        if (_cache.TryGetValue<Project>(key, out var cached)) return cached;
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (project is not null)
        {
            _cache.Set(key, project, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = SlugCacheTtl });
        }
        return project;
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
        await _db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public void InvalidateSlugCache(string slug) => _cache.Remove($"proj:slug:{slug}");
}
