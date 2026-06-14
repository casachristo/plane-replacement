using Waypoint.Api.Subsystems.Identity.Tokens;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Auth;

/// <summary>
/// Writes one token_audit_log row per request whose principal is an InternalService.
/// Captures the passthrough actor headers so audit can attribute the underlying agent.
/// Persistence goes through the Tokens subsystem (ITokenService); the middleware only shapes
/// the audit row from the request/response.
/// </summary>
public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    public AuditLogMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ITokenService tokens)
    {
        await _next(ctx);

        var principal = ctx.GetPrincipal();
        if (principal is null || principal.Kind != PrincipalKind.InternalService) return;
        if (!Guid.TryParse(principal.Id, out var tokenId)) return;

        var entry = new TokenAuditLog
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
        };
        try { await tokens.RecordAuditAsync(entry, ctx.RequestAborted); } catch { /* audit must not fail the request */ }
    }
}
