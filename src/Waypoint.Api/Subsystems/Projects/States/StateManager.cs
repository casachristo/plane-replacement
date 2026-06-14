using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Projects.States;

// Manager — owns State rows. Seeds a project's default workflow states and lists them.
// The only thing that persists State; private to the States feature.
public interface IStateManager
{
    // Seeds the default To Do / In Progress / Done set; returns the Id of the default ("To Do")
    // landing state so the caller can point the project's DefaultStateId at it.
    Task<Guid> SeedDefaultsAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<State>> ListByProjectAsync(Guid projectId, CancellationToken ct);
}

public sealed class StateManager(WaypointDbContext db) : IStateManager
{
    public async Task<Guid> SeedDefaultsAsync(Guid projectId, CancellationToken ct)
    {
        // No Backlog: new projects get a simple To Do / In Progress / Done workflow, with
        // To Do as the default landing state for new issues.
        var todo = new State
        {
            ProjectId = projectId, Name = "To Do", Group = StateGroup.Unstarted,
            Color = "#94a3b8", SortOrder = 0, IsDefault = true,
        };
        var inProgress = new State
        {
            ProjectId = projectId, Name = "In Progress", Group = StateGroup.Started,
            Color = "#3b82f6", SortOrder = 1, IsDefault = false,
        };
        var done = new State
        {
            ProjectId = projectId, Name = "Done", Group = StateGroup.Completed,
            Color = "#22c55e", SortOrder = 2, IsDefault = false,
        };
        db.States.AddRange(todo, inProgress, done);
        await db.SaveChangesAsync(ct);
        return todo.Id;
    }

    public async Task<IReadOnlyList<State>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.States.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
}
