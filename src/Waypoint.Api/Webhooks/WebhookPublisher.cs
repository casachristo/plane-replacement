using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Webhooks;

/// <summary>
/// WAY-6: turns a domain event into one WebhookDelivery row per matching subscription.
/// Caller doesn't SaveChanges — Publisher only stages, the surrounding write (e.g.
/// IssueRepository.TransitionAsync) flushes everything together so a failed primary
/// write doesn't leave orphan deliveries.
/// </summary>
public interface IWebhookPublisher
{
    Task PublishAsync(WebhookEvent evt, Guid? projectId, object payload, CancellationToken ct);
}

public sealed class WebhookPublisher : IWebhookPublisher
{
    /// <summary>Wire-format version of the webhook envelope. Bump on a breaking payload change;
    /// documented in docs/webhooks.md.</summary>
    public const int WebhookEnvelopeVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly WaypointDbContext _db;
    public WebhookPublisher(WaypointDbContext db) => _db = db;

    public async Task PublishAsync(WebhookEvent evt, Guid? projectId, object payload, CancellationToken ct)
    {
        var matches = await _db.WebhookSubscriptions.AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => s.ProjectId == null || s.ProjectId == projectId)
            .Where(s => (s.EventMask & (long)evt) != 0)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (matches.Count == 0) return;

        var envelope = new
        {
            version = WebhookEnvelopeVersion,   // WAY-6: payload schema is versioned (see docs/webhooks.md)
            @event = WebhookEventNames.Wire(evt),
            occurred_at = DateTimeOffset.UtcNow,
            project_id = projectId,
            payload,
        };
        var json = JsonSerializer.Serialize(envelope, Json);

        foreach (var subId in matches)
        {
            _db.WebhookDeliveries.Add(new WebhookDelivery
            {
                SubscriptionId = subId,
                Event = WebhookEventNames.Wire(evt),
                PayloadJson = json,
            });
        }
    }
}
