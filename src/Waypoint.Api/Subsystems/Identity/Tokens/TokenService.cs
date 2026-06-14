using Waypoint.Api.Auth;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Identity.Tokens;

// Service — the stateless facade over the Tokens feature. Owns bearer parsing/verification,
// token creation/validation, and the admin list/revoke/audit surface. The principal resolver
// and admin endpoints depend on this, never on the Manager or the DbContext.
public interface ITokenService
{
    // Parse a raw "Bearer wpt_..." header and return the matching active token, or null if the
    // header is malformed or no active Service/Admin token verifies against it.
    Task<ApiToken?> VerifyBearerAsync(string rawAuthorizationHeader, CancellationToken ct);

    // The effective scopes a token grants — Admin tokens get the synthetic "admin" wildcard so
    // RequireScope("admin") works without per-token plumbing (the tier IS the policy).
    string[] EffectiveScopes(ApiToken token);

    Task<(ApiToken token, string fullToken)> CreateAsync(string name, string[] scopes, string kind, CancellationToken ct);
    Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TokenAuditLog>> ListAuditAsync(Guid? tokenId, DateTimeOffset? since, CancellationToken ct);
    Task RecordAuditAsync(TokenAuditLog entry, CancellationToken ct);
}

public sealed class TokenService(ITokenManager manager) : ITokenService
{
    private const int AuditPageSize = 500;

    public async Task<ApiToken?> VerifyBearerAsync(string rawAuthorizationHeader, CancellationToken ct)
    {
        if (!rawAuthorizationHeader.StartsWith("Bearer wpt_", StringComparison.Ordinal)) return null;
        var bearer = rawAuthorizationHeader["Bearer ".Length..];
        // Format: "wpt_<8-char prefix>_<secret>". The prefix is secret[..8] and may itself
        // contain '_' (base64url alphabet), so slice it by fixed offset rather than splitting
        // on '_' — otherwise tokens whose prefix contains '_' are wrongly rejected.
        if (bearer.Length < 13 || bearer[12] != '_') return null;
        var prefix = bearer.Substring(4, 8);

        foreach (var token in await manager.FindActiveServiceCandidatesByPrefixAsync(prefix, ct))
            if (TokenHasher.Verify(bearer, token.TokenHash))
                return token;
        return null;
    }

    public string[] EffectiveScopes(ApiToken token) =>
        token.Kind == TokenKind.Admin
            ? token.Scopes.Append("admin").Distinct(StringComparer.Ordinal).ToArray()
            : token.Scopes;

    public async Task<(ApiToken token, string fullToken)> CreateAsync(string name, string[] scopes, string kind, CancellationToken ct)
    {
        if (!Enum.TryParse<TokenKind>(kind, ignoreCase: true, out var parsedKind))
            throw new ValidationException("invalid_kind", $"Unknown token kind: {kind}");
        var (prefix, fullToken) = TokenHasher.GenerateNew();
        var token = new ApiToken
        {
            Name = name, Prefix = prefix, TokenHash = TokenHasher.Hash(fullToken),
            Scopes = scopes, Kind = parsedKind,
        };
        await manager.AddAsync(token, ct);
        return (token, fullToken);
    }

    public Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken ct) => manager.ListAsync(ct);

    public Task RevokeAsync(Guid id, CancellationToken ct) => manager.RevokeAsync(id, ct);

    public Task<IReadOnlyList<TokenAuditLog>> ListAuditAsync(Guid? tokenId, DateTimeOffset? since, CancellationToken ct) =>
        manager.ListAuditAsync(tokenId, since, AuditPageSize, ct);

    public Task RecordAuditAsync(TokenAuditLog entry, CancellationToken ct) => manager.AddAuditAsync(entry, ct);
}
