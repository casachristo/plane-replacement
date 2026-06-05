namespace Waypoint.Domain.Enums;

/// <summary>
/// WAY-5: tier of an ApiToken.
/// - Service: per-agent tokens. Whatever scopes are explicitly granted is the
///   policy. Default at mint time.
/// - Admin: ops/bootstrap tokens. Synthetic "admin" scope is added at resolve
///   time so RequireScope("admin") passes without per-token scope plumbing,
///   while still leaving the audit log able to record the tier per request.
/// </summary>
public enum TokenKind
{
    Service = 0,
    Admin   = 1,
}
