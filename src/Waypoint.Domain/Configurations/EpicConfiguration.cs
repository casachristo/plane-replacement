using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class EpicConfiguration : IEntityTypeConfiguration<Epic>
{
    public void Configure(EntityTypeBuilder<Epic> builder)
    {
        builder.ToTable("epics");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.Title).HasMaxLength(500).IsRequired();
        builder.Property(e => e.DescriptionMd).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(50);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(e => e.Project).WithMany().HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.TargetCycle).WithMany().HasForeignKey(e => e.TargetCycleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.ProjectId, e.SequenceId }).IsUnique();
        builder.HasQueryFilter(e => e.DeletedAt == null);
    }
}
