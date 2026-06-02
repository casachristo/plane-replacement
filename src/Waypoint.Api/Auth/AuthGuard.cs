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
}

public sealed class UnauthorizedException(string code, string message)
    : WaypointException(code, message, 401);
