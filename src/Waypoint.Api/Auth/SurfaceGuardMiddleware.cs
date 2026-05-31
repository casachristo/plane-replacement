using System.Text.Json;
using Waypoint.Contracts;

namespace Waypoint.Api.Auth;

/// <summary>
/// Rejects requests whose credentials don't match the surface implied by the URL prefix.
/// /api/v1/*       — public surface — rejects Bearer wpt_* tokens (401 not_for_public_api)
/// /internal/v1/*  — internal surface — rejects session cookies (401 not_for_internal_api)
///
/// Surface enforcement at the app layer is defense-in-depth; the real boundary is the K8s
/// NetworkPolicy that restricts :8081 to Cairn pods.
/// </summary>
public sealed class SurfaceGuardMiddleware
{
    public const string CookieName = "waypoint_session";
    private readonly RequestDelegate _next;

    public SurfaceGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        var hasBearer = ctx.Request.Headers.Authorization.ToString()
            .StartsWith("Bearer wpt_", StringComparison.Ordinal);
        var hasCookie = ctx.Request.Cookies.ContainsKey(CookieName);

        if (path.StartsWith("/api/v1/", StringComparison.Ordinal) && hasBearer)
        {
            await Reject(ctx, "not_for_public_api",
                "This endpoint accepts browser sessions only. Use the internal surface for service tokens.");
            return;
        }
        if (path.StartsWith("/internal/v1/", StringComparison.Ordinal) && hasCookie)
        {
            await Reject(ctx, "not_for_internal_api",
                "This endpoint accepts service tokens only. Use the public surface for browser sessions.");
            return;
        }

        await _next(ctx);
    }

    private static async Task Reject(HttpContext ctx, string code, string message)
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.ContentType = "application/json";
        var requestId = ctx.Items["RequestId"]?.ToString() ?? Guid.NewGuid().ToString();
        var envelope = new ErrorResponse(new ErrorBody(code, message), requestId);
        await JsonSerializer.SerializeAsync(ctx.Response.Body, envelope);
    }
}
