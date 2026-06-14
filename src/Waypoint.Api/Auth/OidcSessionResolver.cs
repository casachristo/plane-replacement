using Microsoft.Extensions.Options;
using Waypoint.Api.Subsystems.Identity.Sessions;
using Waypoint.Domain;

namespace Waypoint.Api.Auth;

/// <summary>
/// Principal resolver (resolution-chain infrastructure): resolves a human Principal from the
/// waypoint_session cookie. Cookie hashing, the user_sessions lookup, and group→scope derivation
/// live in the Sessions subsystem (ISessionService); this resolver only reads the cookie.
/// Session creation happens in the /auth/post-login endpoint after OIDC exchange.
/// </summary>
public sealed class OidcSessionResolver(ISessionService sessions) : IPrincipalResolver
{
    public const string CookieName = "waypoint_session";

    // Composition seam for unit tests that construct the resolver straight from a DbContext.
    public OidcSessionResolver(WaypointDbContext db, IOptions<OidcOptions> options)
        : this(new SessionService(new SessionManager(db), options)) { }

    // The session cookie hash function lives in the Sessions service; re-exposed here so the
    // auth flow's existing callers (and tests) keep a single canonical hashing entry point.
    public static string HashCookie(string cookieValue) => SessionService.HashCookie(cookieValue);

    public async Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct)
    {
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var cookieValue) || string.IsNullOrEmpty(cookieValue))
            return null;
        return await sessions.ResolvePrincipalAsync(cookieValue, ct);
    }
}
