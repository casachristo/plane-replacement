using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;

namespace Waypoint.Api.Webhooks;

/// <summary>
/// IHostedService that polls webhook_deliveries for pending rows, POSTs them to the
/// target URL, and updates status/retry. Backoff schedule: 1m, 5m, 30m, 2h, 12h, then
/// dead-letter. The poll interval is 30s; for low-volume homelab use this is fine.
/// </summary>
public sealed class WebhookDispatcher : BackgroundService
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    ];
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory,
        ILogger<WebhookDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchPendingAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "WebhookDispatcher iteration failed"); }
            try { await Task.Delay(PollInterval, stoppingToken); } catch (TaskCanceledException) { }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        var now = DateTimeOffset.UtcNow;

        var due = await db.WebhookDeliveries
            .Include(d => d.Subscription)
            .Where(d => d.Status == "pending" && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var delivery in due)
        {
            if (!delivery.Subscription.IsActive) { delivery.Status = "cancelled"; continue; }
            await TryDeliverAsync(delivery, db, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task TryDeliverAsync(Domain.Entities.WebhookDelivery delivery, WaypointDbContext db, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("waypoint-webhooks");
        var content = new StringContent(delivery.PayloadJson, System.Text.Encoding.UTF8, "application/json");
        content.Headers.TryAddWithoutValidation("X-Waypoint-Signature", WebhookSigner.Sign(delivery.Subscription.Secret, delivery.PayloadJson));
        content.Headers.TryAddWithoutValidation("X-Waypoint-Event", delivery.Event);
        content.Headers.TryAddWithoutValidation("X-Waypoint-Delivery-Id", delivery.Id.ToString());

        try
        {
            using var resp = await http.PostAsync(delivery.Subscription.TargetUrl, content, ct);
            delivery.AttemptedAt = DateTimeOffset.UtcNow;
            delivery.ResponseCode = (int)resp.StatusCode;
            delivery.AttemptN++;
            if (resp.IsSuccessStatusCode)
            {
                delivery.Status = "success";
                delivery.Subscription.LastDeliveryAt = delivery.AttemptedAt;
            }
            else
            {
                ScheduleRetryOrDeadLetter(delivery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook delivery {DeliveryId} failed", delivery.Id);
            delivery.AttemptedAt = DateTimeOffset.UtcNow;
            delivery.AttemptN++;
            ScheduleRetryOrDeadLetter(delivery);
        }
    }

    private static void ScheduleRetryOrDeadLetter(Domain.Entities.WebhookDelivery delivery)
    {
        if (delivery.AttemptN >= Backoff.Length)
        {
            delivery.Status = "dead-letter";
            delivery.NextAttemptAt = null;
        }
        else
        {
            delivery.Status = "pending";
            delivery.NextAttemptAt = DateTimeOffset.UtcNow + Backoff[delivery.AttemptN];
        }
    }
}
