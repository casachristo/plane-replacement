using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Auth;

public sealed class ServiceBearerResolver : IPrincipalResolver
{
    private readonly WaypointDbContext _db;

    public ServiceBearerResolver(WaypointDbContext db) => _db = db;

    public async Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct)
    {
        if (!ctx.Request.Headers.TryGetValue("Authorization", out var authHeader)) return null;
        var raw = authHeader.ToString();
        if (!raw.StartsWith("Bearer wpt_", StringComparison.Ordinal)) return null;

        // Format: "Bearer wpt_<8charPrefix>_<secret>"
        var bearer = raw["Bearer ".Length..];
        var parts = bearer.Split('_', 3);
        if (parts.Length != 3 || parts[0] != "wpt" || parts[1].Length != 8) return null;
        var prefix = parts[1];

        // WAY-5: both Service AND Admin tiers are accepted here. Admin tokens get a
        // synthetic "admin" scope so RequireScope("admin") works without per-token
        // scope plumbing — the tier IS the policy on admin endpoints.
        var candidates = await _db.ApiTokens.AsNoTracking()
            .Where(t => t.Prefix == prefix && t.RevokedAt == null
                        && (t.Kind == TokenKind.Service || t.Kind == TokenKind.Admin))
            .ToListAsync(ct);

        foreach (var token in candidates)
        {
            if (TokenHasher.Verify(bearer, token.TokenHash))
            {
                ctx.Request.Headers.TryGetValue("X-On-Behalf-Of", out var passthroughId);
                ctx.Request.Headers.TryGetValue("X-On-Behalf-Of-Label", out var passthroughLabel);

                var scopes = token.Kind == TokenKind.Admin
                    ? token.Scopes.Append("admin").Distinct(StringComparer.Ordinal).ToArray()
                    : token.Scopes;

                return new Principal(
                    Kind: PrincipalKind.InternalService,
                    Id: token.Id.ToString(),
                    DisplayName: token.Name,
                    Scopes: scopes,
                    PassthroughActorId: string.IsNullOrEmpty(passthroughId) ? null : passthroughId.ToString(),
                    PassthroughActorLabel: string.IsNullOrEmpty(passthroughLabel) ? null : passthroughLabel.ToString());
            }
        }
        return null;
    }
}
