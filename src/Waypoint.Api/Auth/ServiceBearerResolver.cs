using Waypoint.Api.Subsystems.Identity.Tokens;
using Waypoint.Domain;

namespace Waypoint.Api.Auth;

// Principal resolver (resolution-chain infrastructure): turns an "Authorization: Bearer wpt_..."
// header into an InternalService principal. Token lookup/verification + the effective-scope policy
// live in the Tokens subsystem (ITokenService); this resolver only reads the request headers and
// shapes the Principal (including the on-behalf-of passthrough actor).
public sealed class ServiceBearerResolver(ITokenService tokens) : IPrincipalResolver
{
    // Composition seam for unit tests that construct the resolver straight from a DbContext.
    public ServiceBearerResolver(WaypointDbContext db) : this(new TokenService(new TokenManager(db))) { }

    public async Task<Principal?> ResolveAsync(HttpContext ctx, CancellationToken ct)
    {
        if (!ctx.Request.Headers.TryGetValue("Authorization", out var authHeader)) return null;
        var token = await tokens.VerifyBearerAsync(authHeader.ToString(), ct);
        if (token is null) return null;

        ctx.Request.Headers.TryGetValue("X-On-Behalf-Of", out var passthroughId);
        ctx.Request.Headers.TryGetValue("X-On-Behalf-Of-Label", out var passthroughLabel);

        return new Principal(
            Kind: PrincipalKind.InternalService,
            Id: token.Id.ToString(),
            DisplayName: token.Name,
            Scopes: tokens.EffectiveScopes(token),
            PassthroughActorId: string.IsNullOrEmpty(passthroughId) ? null : passthroughId.ToString(),
            PassthroughActorLabel: string.IsNullOrEmpty(passthroughLabel) ? null : passthroughLabel.ToString(),
            TokenKind: token.Kind);
    }
}
