namespace Waypoint.Api.Auth;

public sealed record Principal(
    PrincipalKind Kind,
    string Id,
    string DisplayName,
    IReadOnlyList<string> Scopes,
    string? PassthroughActorId = null,
    string? PassthroughActorLabel = null);

public enum PrincipalKind { Human, InternalService }
