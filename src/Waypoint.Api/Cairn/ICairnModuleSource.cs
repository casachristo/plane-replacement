namespace Waypoint.Api.Cairn;

/// <summary>
/// WAY-15: supplies the module names (board swimlane rows) for a Cairn-linked project from
/// Cairn's architecture catalog. Phase 1 ships the contract + a Null implementation; the live
/// HTTP source against Cairn is a later phase, swapped in via DI without touching callers.
/// </summary>
public interface ICairnModuleSource
{
    Task<IReadOnlyList<string>> GetModulesAsync(string cairnProjectName, CancellationToken ct);
}

/// <summary>Default: no modules. A linked project still reports CairnLinked=true, but renders
/// no extra rows until a concrete source is configured.</summary>
public sealed class NullCairnModuleSource : ICairnModuleSource
{
    public Task<IReadOnlyList<string>> GetModulesAsync(string cairnProjectName, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(System.Array.Empty<string>());
}
