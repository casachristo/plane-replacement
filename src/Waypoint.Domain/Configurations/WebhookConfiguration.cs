using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.TargetUrl).HasMaxLength(2000).IsRequired();
        builder.Property(s => s.Secret).HasMaxLength(128).IsRequired();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(s => s.Project).WithMany().HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.Event).HasMaxLength(100).IsRequired();
        builder.Property(d => d.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.Status).HasMaxLength(20);
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(d => d.Subscription).WithMany().HasForeignKey(d => d.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
        builder.HasIndex(d => d.SubscriptionId);
    }
}
