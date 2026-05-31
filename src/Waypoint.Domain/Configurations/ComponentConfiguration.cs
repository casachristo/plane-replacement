using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> builder)
    {
        builder.ToTable("components");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(c => c.Project).WithMany().HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.OwnerUser).WithMany().HasForeignKey(c => c.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(c => new { c.ProjectId, c.Name }).IsUnique();
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}

public class IssueComponentConfiguration : IEntityTypeConfiguration<IssueComponent>
{
    public void Configure(EntityTypeBuilder<IssueComponent> builder)
    {
        builder.ToTable("issue_components");
        builder.HasKey(ic => new { ic.IssueId, ic.ComponentId });
        builder.HasOne(ic => ic.Issue).WithMany().HasForeignKey(ic => ic.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ic => ic.Component).WithMany().HasForeignKey(ic => ic.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
