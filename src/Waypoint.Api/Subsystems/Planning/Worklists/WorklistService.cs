using Waypoint.Api.Endpoints;
using Waypoint.Contracts;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Planning.Worklists;

// Service — stateless facade over the Worklist feature. The internal endpoints (auth + project
// resolution handled there) call this; it drives the Manager and maps to the wire DTOs. Also
// exposes SeedAsync for the Projects provisioning Orchestrator.
public interface IWorklistService
{
    Task SeedAsync(Guid projectId, CancellationToken ct);
    Task<WorklistStatusDto> GetAsync(Guid projectId, CancellationToken ct);
    Task<WorklistStatusDto> StartAsync(Guid projectId, CancellationToken ct);
    Task<WorklistStatusDto> AdvanceAsync(Guid projectId, CancellationToken ct);
    Task<WorklistStatusDto> SkipAsync(Guid projectId, string reason, CancellationToken ct);
    Task<WorklistStopSummary> StopAsync(Guid projectId, CancellationToken ct);
}

public sealed class WorklistService(IWorklistManager manager) : IWorklistService
{
    public Task SeedAsync(Guid projectId, CancellationToken ct) => manager.SeedAsync(projectId, ct);

    public async Task<WorklistStatusDto> GetAsync(Guid projectId, CancellationToken ct) =>
        ToStatus(await manager.GetAsync(projectId, ct));

    public async Task<WorklistStatusDto> StartAsync(Guid projectId, CancellationToken ct) =>
        ToStatus(await manager.StartAsync(projectId, ct));

    public async Task<WorklistStatusDto> AdvanceAsync(Guid projectId, CancellationToken ct) =>
        ToStatus(await manager.AdvanceAsync(projectId, ct));

    public async Task<WorklistStatusDto> SkipAsync(Guid projectId, string reason, CancellationToken ct) =>
        ToStatus(await manager.SkipAsync(projectId, reason, ct));

    public async Task<WorklistStopSummary> StopAsync(Guid projectId, CancellationToken ct)
    {
        var wl = await manager.StopAsync(projectId, ct);
        return new WorklistStopSummary(wl.DoneCount, wl.SkippedCount, wl.RemainingCount);
    }

    private static WorklistStatusDto ToStatus((Worklist wl, Issue? current) r) => new(
        State: r.wl.State.ToString().ToLowerInvariant(),
        Current: r.current is null ? null : IssueMapper.ToDto(r.current),
        RemainingCount: r.wl.RemainingCount,
        DoneCount: r.wl.DoneCount,
        SkippedCount: r.wl.SkippedCount,
        StartedAt: r.wl.StartedAt,
        CompletedAt: r.wl.CompletedAt);
}
