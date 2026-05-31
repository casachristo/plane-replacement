using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(500);
        builder.Property(w => w.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(w => w.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(w => w.Project).WithMany().HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(w => new { w.ProjectId, w.Name }).IsUnique();
        builder.HasQueryFilter(w => w.DeletedAt == null);
    }
}
