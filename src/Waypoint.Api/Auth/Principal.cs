using Waypoint.Domain.Enums;

namespace Waypoint.Api.Auth;

public sealed record Principal(
    PrincipalKind Kind,
    string Id,
    string DisplayName,
    IReadOnlyList<string> Scopes,
    string? PassthroughActorId = null,
    string? PassthroughActorLabel = null,
    // WAY-5: tier of the service token behind this principal (null for human/OIDC principals).
    // Carried here so the audit log records the tier per call without re-querying the token.
    TokenKind? TokenKind = null);

public enum PrincipalKind { Human, InternalService }
