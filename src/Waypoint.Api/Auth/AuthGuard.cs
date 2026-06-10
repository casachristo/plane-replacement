using Waypoint.Domain;

namespace Waypoint.Api.Auth;

/// <summary>
/// Convenience helpers for endpoint handlers to require an authenticated principal and
/// optionally a specific scope. Throws WaypointException subtypes that the
/// ErrorEnvelopeMiddleware turns into the standard JSON error envelope.
/// </summary>
public static class AuthGuard
{
    public static Principal RequireAuth(HttpContext ctx) =>
        ctx.GetPrincipal()
            ?? throw new UnauthorizedException("unauthenticated", "Sign in or present a service token.");

    public static Principal RequireScope(HttpContext ctx, string scope)
    {
        var p = RequireAuth(ctx);
        if (!p.Scopes.Contains(scope, StringComparer.Ordinal))
            throw new ValidationException("missing_scope", $"Required scope: {scope}.",
                new Dictionary<string, object> { ["required"] = scope });
        return p;
    }

    /// <summary>
    /// WAY-19: only credentials holding "issue:transition" (or the all-powerful "admin" tier)
    /// may move an issue between states. A WRITER token — which can create/edit issue fields —
    /// is forbidden (403) from transitioning. This is the structural half of the transition
    /// gate: external/agent callers hold writer tokens and must route state changes through
    /// Cairn's /tasks/{id}/transition (which uses an admin token), never via a direct PATCH.
    /// 403 (not 422) is deliberate — Cairn's gate keys on the forbidden status.
    /// </summary>
    public static Principal RequireTransitionRights(HttpContext ctx)
    {
        var p = RequireAuth(ctx);
        if (p.Scopes.Contains("admin", StringComparer.Ordinal) ||
            p.Scopes.Contains("issue:transition", StringComparer.Ordinal))
            return p;
        throw new ForbiddenException("transition_forbidden",
            "This credential cannot transition issue state; route the change through Cairn.");
    }
}

public sealed class UnauthorizedException(string code, string message)
    : WaypointException(code, message, 401);

public sealed class ForbiddenException(string code, string message)
    : WaypointException(code, message, 403);
