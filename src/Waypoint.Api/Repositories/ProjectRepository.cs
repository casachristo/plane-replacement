using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly WaypointDbContext _db;
    public ProjectRepository(WaypointDbContext db) => _db = db;

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

    public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct) =>
        _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
        await _db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
}
