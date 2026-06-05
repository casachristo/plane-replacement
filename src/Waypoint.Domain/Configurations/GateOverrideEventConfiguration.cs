using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class GateOverrideEventConfiguration : IEntityTypeConfiguration<GateOverrideEvent>
{
    public void Configure(EntityTypeBuilder<GateOverrideEvent> builder)
    {
        builder.ToTable("gate_override_events");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(g => g.GateName).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(g => g.ActorType).HasConversion<int>();
        builder.Property(g => g.ActorLabel).HasMaxLength(200);
        builder.Property(g => g.At).HasDefaultValueSql("now()");
        builder.HasOne(g => g.Issue).WithMany().HasForeignKey(g => g.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        // Queryable independently of activities: by gate name + recency.
        builder.HasIndex(g => new { g.GateName, g.At });
        builder.HasIndex(g => g.IssueId);
    }
}
