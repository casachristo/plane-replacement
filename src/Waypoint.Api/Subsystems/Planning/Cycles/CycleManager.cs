using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Planning.Cycles;

// Manager — owns Cycle state. Lists a project's cycles and checks cycle membership (used when
// an issue is assigned to a cycle). The only thing that persists/queries Cycle; private to the
// Cycles feature. (Cycle creation has no API surface yet — tracked as a follow-up.)
public interface ICycleManager
{
    Task<IReadOnlyList<Cycle>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task<bool> ExistsInProjectAsync(Guid cycleId, Guid projectId, CancellationToken ct);
}

public sealed class CycleManager(WaypointDbContext db) : ICycleManager
{
    public async Task<IReadOnlyList<Cycle>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.Set<Cycle>().AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.DeletedAt == null)
            .OrderBy(c => c.StartDate)
            .ToListAsync(ct);

    public Task<bool> ExistsInProjectAsync(Guid cycleId, Guid projectId, CancellationToken ct) =>
        db.Set<Cycle>().AnyAsync(c => c.Id == cycleId && c.ProjectId == projectId, ct);
}
