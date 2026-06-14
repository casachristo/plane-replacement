using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Waypoint.Domain;

namespace Waypoint.Api.Auth;

/// <summary>
/// Resolves a Principal from the waypoint_session cookie. The cookie value is hashed
/// (SHA-256) and looked up in user_sessions; on a hit the user's groups → scopes are returned.
/// Session creation happens in the /auth/callback endpoint after OIDC exchange.
/// </summary>
public sealed class OidcSessionResolver : IPrincipalResolver
{
    public const string CookieName = "waypoint_session";

    private readonly WaypointDbContext _db;
    private readonly OidcOptions _options;

    public OidcSessionResolver(WaypointDbContext db, IOptions<OidcOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct)
    {
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var cookieValue) || string.IsNullOrEmpty(cookieValue))
            return null;

        var hash = HashCookie(cookieValue);
        var session = await _db.UserSessions.AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.CookieHash == hash && s.ExpiresAt > DateTimeOffset.UtcNow, ct);
        if (session is null) return null;

        var scopes = DeriveScopes(session.User.Groups);
        return new Principal(
            Kind: PrincipalKind.Human,
            Id: session.User.Id.ToString(),
            DisplayName: session.User.DisplayName,
            Scopes: scopes);
    }

    public static string HashCookie(string cookieValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(cookieValue));
        return Convert.ToHexString(bytes);
    }

    private List<string> DeriveScopes(string[]? groups)
    {
        var scopes = new List<string> { "issue:read", "issue:create", "issue:write", "issue:transition", "comment:create" };
        if (groups is not null && groups.Any(g => _options.AdminGroups.Contains(g, StringComparer.Ordinal)))
            scopes.Add("admin");
        return scopes;
    }
}
