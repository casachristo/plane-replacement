using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Projects.Labels;

// Manager — owns Label rows. Lists a project's (non-deleted) labels. The only thing that
// persists Label; private to the Labels feature.
public interface ILabelManager
{
    Task<IReadOnlyList<Label>> ListByProjectAsync(Guid projectId, CancellationToken ct);
}

public sealed class LabelManager(WaypointDbContext db) : ILabelManager
{
    public async Task<IReadOnlyList<Label>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.Set<Label>().AsNoTracking()
            .Where(l => l.ProjectId == projectId && l.DeletedAt == null)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);
}
