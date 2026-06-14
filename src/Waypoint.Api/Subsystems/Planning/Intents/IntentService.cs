namespace Waypoint.Api.Subsystems.Planning.Intents;

// Service — stateless facade over the Intents feature. The internal endpoints (auth + project
// resolution handled there) call this; it delegates to the Manager and returns the intent id.
public interface IIntentService
{
    Task<Guid> FileAsync(Guid projectId, string modulePath, string intentText, Guid declaredByTokenId, CancellationToken ct);
    Task ReleaseAsync(Guid intentId, int? linkedIssueSeq, CancellationToken ct);
}

public sealed class IntentService(IIntentManager manager) : IIntentService
{
    public async Task<Guid> FileAsync(Guid projectId, string modulePath, string intentText, Guid declaredByTokenId, CancellationToken ct) =>
        (await manager.FileAsync(projectId, modulePath, intentText, declaredByTokenId, ct)).Id;

    public Task ReleaseAsync(Guid intentId, int? linkedIssueSeq, CancellationToken ct) =>
        manager.ReleaseAsync(intentId, linkedIssueSeq, ct);
}
