using Waypoint.Api.Auth;
using Waypoint.Api.Subsystems.Identity.Tokens;

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

        tokens.MapGet("/", async (ITokenService svc, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var list = (await svc.ListAsync(ct))
                .Select(t => new ApiTokenDto(t.Id, t.Name, t.Prefix, t.Scopes, t.Kind.ToString(),
                    t.LastUsedAt, t.RevokedAt, t.CreatedAt))
                .ToList();
            return Results.Ok(list);
        });

        tokens.MapPost("/", async (CreateApiTokenRequest req, ITokenService svc, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var (token, fullToken) = await svc.CreateAsync(req.Name, req.Scopes, req.Kind, ct);
            var dto = new ApiTokenDto(token.Id, token.Name, token.Prefix, token.Scopes, token.Kind.ToString(),
                null, null, token.CreatedAt);
            return Results.Created($"/api/admin/tokens/{token.Id}", new ApiTokenCreatedDto(dto, fullToken));
        });

        tokens.MapDelete("/{id:guid}", async (Guid id, ITokenService svc, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            await svc.RevokeAsync(id, ct);
            return Results.NoContent();
        });

        app.MapGet("/api/admin/audit", async (Guid? tokenId, DateTimeOffset? since,
            ITokenService svc, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var rows = (await svc.ListAuditAsync(tokenId, since, ct))
                .Select(a => new
                {
                    a.Id, a.TokenId, a.PassthroughActorId, a.PassthroughActorLabel,
                    a.Action, a.Path, a.Method, a.Ip, a.StatusCode, a.At,
                })
                .ToList();
            return Results.Ok(rows);
        });
    }

    private static void RequireAdmin(HttpContext ctx) => Waypoint.Api.Auth.AuthGuard.RequireScope(ctx, "admin");
}
