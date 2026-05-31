using System.Text.Json;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Middleware;

public sealed class ErrorEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorEnvelopeMiddleware> _logger;

    public ErrorEnvelopeMiddleware(RequestDelegate next, ILogger<ErrorEnvelopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (WaypointException ex)
        {
            await WriteEnvelope(ctx, ex.StatusCode, ex.Code, ex.Message, ex.Details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteEnvelope(ctx, 500, "internal_error", "An unexpected error occurred.", null);
        }
    }

    private static async Task WriteEnvelope(HttpContext ctx, int status, string code, string message, IReadOnlyDictionary<string, object>? details)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var requestId = ctx.Items["RequestId"]?.ToString() ?? Guid.NewGuid().ToString();
        var envelope = new ErrorResponse(new ErrorBody(code, message, details), requestId);
        await JsonSerializer.SerializeAsync(ctx.Response.Body, envelope);
    }
}
