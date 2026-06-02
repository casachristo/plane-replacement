using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Endpoints.PublicApi;

public sealed record CreateApiTokenRequest(string Name, string[] Scopes, string Kind);
public sealed record ApiTokenDto(Guid Id, string Name, string Prefix, string[] Scopes, string Kind,
    DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, DateTimeOffset CreatedAt);
public sealed record ApiTokenCreatedDto(ApiTokenDto Token, string FullToken);

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var tokens = app.MapGroup("/api/admin/tokens");

        tokens.MapGet("/", async (WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var list = await db.ApiTokens.AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new ApiTokenDto(t.Id, t.Name, t.Prefix, t.Scopes, t.Kind.ToString(),
                    t.LastUsedAt, t.RevokedAt, t.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        tokens.MapPost("/", async (CreateApiTokenRequest req, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            if (!Enum.TryParse<TokenKind>(req.Kind, ignoreCase: true, out var kind))
                throw new ValidationException("invalid_kind", $"Unknown token kind: {req.Kind}");
            var (prefix, fullToken) = TokenHasher.GenerateNew();
            var token = new ApiToken
            {
                Name = req.Name, Prefix = prefix, TokenHash = TokenHasher.Hash(fullToken),
                Scopes = req.Scopes, Kind = kind,
            };
            db.ApiTokens.Add(token);
            await db.SaveChangesAsync(ct);
            var dto = new ApiTokenDto(token.Id, token.Name, token.Prefix, token.Scopes, token.Kind.ToString(),
                null, null, token.CreatedAt);
            return Results.Created($"/api/admin/tokens/{token.Id}", new ApiTokenCreatedDto(dto, fullToken));
        });

        tokens.MapDelete("/{id:guid}", async (Guid id, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new NotFoundException("token_not_found", "Token not found.");
            token.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        app.MapGet("/api/admin/audit", async (Guid? tokenId, DateTimeOffset? since,
            WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var query = db.TokenAuditLog.AsNoTracking().AsQueryable();
            if (tokenId is not null) query = query.Where(a => a.TokenId == tokenId);
            if (since is not null) query = query.Where(a => a.At >= since);
            var rows = await query
                .OrderByDescending(a => a.At)
                .Take(500)
                .Select(a => new
                {
                    a.Id, a.TokenId, a.PassthroughActorId, a.PassthroughActorLabel,
                    a.Action, a.Path, a.Method, a.Ip, a.StatusCode, a.At,
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        });
    }

    private static void RequireAdmin(HttpContext ctx) => Waypoint.Api.Auth.AuthGuard.RequireScope(ctx, "admin");
}
