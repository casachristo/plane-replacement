using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Planning.Epics;

// Manager — owns Epic state. Lists a project's epics and creates one with the next per-project
// sequence. The only thing that persists Epic; private to the Epics feature.
public interface IEpicManager
{
    Task<IReadOnlyList<Epic>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task<Epic> CreateAsync(Guid projectId, string title, CancellationToken ct);
}

public sealed class EpicManager(WaypointDbContext db) : IEpicManager
{
    public async Task<IReadOnlyList<Epic>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.Set<Epic>().AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.SequenceId)
            .ToListAsync(ct);

    public async Task<Epic> CreateAsync(Guid projectId, string title, CancellationToken ct)
    {
        var maxSeq = await db.Set<Epic>().Where(e => e.ProjectId == projectId)
            .MaxAsync(e => (int?)e.SequenceId, ct) ?? 0;
        var epic = new Epic
        {
            ProjectId = projectId, SequenceId = maxSeq + 1, Title = title, Status = "planned",
        };
        db.Set<Epic>().Add(epic);
        await db.SaveChangesAsync(ct);
        return epic;
    }
}
