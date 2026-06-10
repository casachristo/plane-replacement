using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Auth;

/// <summary>
/// Writes one token_audit_log row per request whose principal is an InternalService.
/// Captures the passthrough actor headers so audit can attribute the underlying agent.
/// </summary>
public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    public AuditLogMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, WaypointDbContext db)
    {
        await _next(ctx);

        var principal = ctx.GetPrincipal();
        if (principal is null || principal.Kind != PrincipalKind.InternalService) return;
        if (!Guid.TryParse(principal.Id, out var tokenId)) return;

        db.TokenAuditLog.Add(new TokenAuditLog
        {
            TokenId = tokenId,
            TokenKind = principal.TokenKind,   // WAY-5: record the tier behind this call
            PassthroughActorId = principal.PassthroughActorId,
            PassthroughActorLabel = principal.PassthroughActorLabel,
            Action = $"{ctx.Request.Method} {ctx.Request.Path}",
            Path = ctx.Request.Path,
            Method = ctx.Request.Method,
            Ip = ctx.Connection.RemoteIpAddress?.ToString(),
            StatusCode = ctx.Response.StatusCode,
        });
        try { await db.SaveChangesAsync(ctx.RequestAborted); } catch { /* audit must not fail the request */ }
    }
}
