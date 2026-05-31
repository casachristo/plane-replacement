namespace Waypoint.Domain.Entities;

public class WebhookSubscription
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }     // NULL = workspace-wide
    public Project? Project { get; set; }
    public required string TargetUrl { get; set; }
    public long EventMask { get; set; }      // bitfield of WebhookEvent values
    public required string Secret { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastDeliveryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public WebhookSubscription Subscription { get; set; } = null!;
    public required string Event { get; set; }
    public required string PayloadJson { get; set; }
    public DateTimeOffset? AttemptedAt { get; set; }
    public string Status { get; set; } = "pending";     // pending / success / failed / dead-letter
    public int? ResponseCode { get; set; }
    public int AttemptN { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
