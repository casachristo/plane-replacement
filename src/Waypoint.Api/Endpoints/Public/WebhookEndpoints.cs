using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Endpoints.PublicApi;

public sealed record CreateWebhookSubscriptionRequest(string TargetUrl, long EventMask, Guid? ProjectId);
public sealed record WebhookSubscriptionDto(Guid Id, Guid? ProjectId, string TargetUrl, long EventMask, bool IsActive,
    DateTimeOffset? LastDeliveryAt, DateTimeOffset CreatedAt);
public sealed record WebhookSubscriptionCreatedDto(WebhookSubscriptionDto Subscription, string Secret);

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/webhooks");

        group.MapGet("/", async (WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var subs = await db.WebhookSubscriptions.AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new WebhookSubscriptionDto(s.Id, s.ProjectId, s.TargetUrl, s.EventMask, s.IsActive, s.LastDeliveryAt, s.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(subs);
        });

        group.MapPost("/", async (CreateWebhookSubscriptionRequest req, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var sub = new WebhookSubscription
            {
                ProjectId = req.ProjectId,
                TargetUrl = req.TargetUrl,
                EventMask = req.EventMask,
                Secret = secret,
            };
            db.WebhookSubscriptions.Add(sub);
            await db.SaveChangesAsync(ct);
            var dto = new WebhookSubscriptionDto(sub.Id, sub.ProjectId, sub.TargetUrl, sub.EventMask, sub.IsActive, null, sub.CreatedAt);
            return Results.Created($"/api/v1/webhooks/{sub.Id}", new WebhookSubscriptionCreatedDto(dto, secret));
        });

        group.MapDelete("/{id:guid}", async (Guid id, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var sub = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw new NotFoundException("webhook_not_found", "Subscription not found.");
            sub.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}/deliveries", async (Guid id, WaypointDbContext db, HttpContext ctx, CancellationToken ct) =>
        {
            RequireAdmin(ctx);
            var deliveries = await db.WebhookDeliveries.AsNoTracking()
                .Where(d => d.SubscriptionId == id)
                .OrderByDescending(d => d.CreatedAt)
                .Take(100)
                .Select(d => new { d.Id, d.Event, d.Status, d.AttemptN, d.ResponseCode, d.AttemptedAt, d.NextAttemptAt, d.CreatedAt })
                .ToListAsync(ct);
            return Results.Ok(deliveries);
        });
    }

    private static void RequireAdmin(HttpContext ctx)
    {
        var principal = ctx.GetPrincipal()
            ?? throw new NotFoundException("unauthenticated", "Sign in required.");
        if (!principal.Scopes.Contains("admin"))
            throw new ValidationException("missing_scope", "Admin scope required.");
    }
}
