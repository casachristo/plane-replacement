using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Subsystems.Identity.Tokens;

// Manager — owns ApiToken + TokenAuditLog state. The only thing that persists/queries API
// tokens and their audit trail; private to the Tokens feature. Token hashing/verification is
// the pure TokenHasher utility; this Manager handles the persistence around it.
public interface ITokenManager
{
    // Active Service/Admin tokens whose prefix matches — the candidate set a bearer verifies against.
    Task<IReadOnlyList<ApiToken>> FindActiveServiceCandidatesByPrefixAsync(string prefix, CancellationToken ct);
    Task AddAsync(ApiToken token, CancellationToken ct);
    Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TokenAuditLog>> ListAuditAsync(Guid? tokenId, DateTimeOffset? since, int take, CancellationToken ct);
    Task AddAuditAsync(TokenAuditLog entry, CancellationToken ct);
}

public sealed class TokenManager(WaypointDbContext db) : ITokenManager
{
    public async Task<IReadOnlyList<ApiToken>> FindActiveServiceCandidatesByPrefixAsync(string prefix, CancellationToken ct) =>
        await db.ApiTokens.AsNoTracking()
            .Where(t => t.Prefix == prefix && t.RevokedAt == null
                        && (t.Kind == TokenKind.Service || t.Kind == TokenKind.Admin))
            .ToListAsync(ct);

    public async Task AddAsync(ApiToken token, CancellationToken ct)
    {
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken ct) =>
        await db.ApiTokens.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("token_not_found", "Token not found.");
        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TokenAuditLog>> ListAuditAsync(Guid? tokenId, DateTimeOffset? since, int take, CancellationToken ct)
    {
        var query = db.TokenAuditLog.AsNoTracking().AsQueryable();
        if (tokenId is not null) query = query.Where(a => a.TokenId == tokenId);
        if (since is not null) query = query.Where(a => a.At >= since);
        return await query.OrderByDescending(a => a.At).Take(take).ToListAsync(ct);
    }

    public async Task AddAuditAsync(TokenAuditLog entry, CancellationToken ct)
    {
        db.TokenAuditLog.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
