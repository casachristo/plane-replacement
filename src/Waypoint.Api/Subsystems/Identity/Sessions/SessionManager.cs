using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Identity.Sessions;

// Manager — owns User + UserSession state (the OIDC-backed human login). The only thing that
// persists/queries users and sessions; private to the Sessions feature.
public interface ISessionManager
{
    Task<UserSession?> ResolveByCookieHashAsync(string cookieHash, CancellationToken ct);
    Task<User> UpsertUserAsync(string issuer, string oidcSub, string email, string displayName, string[] groups, CancellationToken ct);
    Task AddSessionAsync(UserSession session, CancellationToken ct);
    Task DeleteByCookieHashAsync(string cookieHash, CancellationToken ct);
}

public sealed class SessionManager(WaypointDbContext db) : ISessionManager
{
    public Task<UserSession?> ResolveByCookieHashAsync(string cookieHash, CancellationToken ct) =>
        db.UserSessions.AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.CookieHash == cookieHash && s.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public async Task<User> UpsertUserAsync(string issuer, string oidcSub, string email, string displayName, string[] groups, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.OidcIssuer == issuer && u.OidcSub == oidcSub, ct);
        if (user is null)
        {
            user = new User { Email = email, DisplayName = displayName, OidcSub = oidcSub, OidcIssuer = issuer, Groups = groups };
            db.Users.Add(user);
        }
        else
        {
            user.Email = email;
            user.DisplayName = displayName;
            user.Groups = groups;
            user.LastLoginAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken ct)
    {
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteByCookieHashAsync(string cookieHash, CancellationToken ct)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.CookieHash == cookieHash, ct);
        if (session is not null) db.UserSessions.Remove(session);
        await db.SaveChangesAsync(ct);
    }
}
