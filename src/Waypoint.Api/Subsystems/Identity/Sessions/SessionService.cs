using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Identity.Scopes;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Subsystems.Identity.Sessions;

// Service — stateless facade over the Sessions feature. Resolves the waypoint_session cookie to
// a human Principal, and drives the OIDC login establish / logout flows. Owns cookie hashing and
// scope derivation (via ScopePolicy); callers pass raw cookie values and never hash themselves.
public interface ISessionService
{
    Task<Principal?> ResolvePrincipalAsync(string cookieValue, CancellationToken ct);
    Task<(string cookieValue, DateTimeOffset expiresAt)> EstablishSessionAsync(
        string issuer, string oidcSub, string email, string displayName, string[] groups,
        string? ip, string? userAgent, CancellationToken ct);
    Task LogoutAsync(string cookieValue, CancellationToken ct);
}

public sealed class SessionService(ISessionManager manager, IOptions<OidcOptions> options) : ISessionService
{
    private readonly OidcOptions _options = options.Value;

    public async Task<Principal?> ResolvePrincipalAsync(string cookieValue, CancellationToken ct)
    {
        var session = await manager.ResolveByCookieHashAsync(HashCookie(cookieValue), ct);
        if (session is null) return null;
        return new Principal(
            Kind: PrincipalKind.Human,
            Id: session.User.Id.ToString(),
            DisplayName: session.User.DisplayName,
            Scopes: ScopePolicy.ForGroups(session.User.Groups, _options.AdminGroups));
    }

    public async Task<(string cookieValue, DateTimeOffset expiresAt)> EstablishSessionAsync(
        string issuer, string oidcSub, string email, string displayName, string[] groups,
        string? ip, string? userAgent, CancellationToken ct)
    {
        var user = await manager.UpsertUserAsync(issuer, oidcSub, email, displayName, groups, ct);
        var cookieValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTimeOffset.UtcNow + _options.SessionLifetime;
        await manager.AddSessionAsync(new UserSession
        {
            UserId = user.Id,
            CookieHash = HashCookie(cookieValue),
            ExpiresAt = expiresAt,
            Ip = ip,
            UserAgent = userAgent,
        }, ct);
        return (cookieValue, expiresAt);
    }

    public Task LogoutAsync(string cookieValue, CancellationToken ct) =>
        manager.DeleteByCookieHashAsync(HashCookie(cookieValue), ct);

    public static string HashCookie(string cookieValue) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookieValue)));
}
