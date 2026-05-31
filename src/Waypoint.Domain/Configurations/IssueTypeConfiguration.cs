using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> builder)
    {
        builder.ToTable("issue_types");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(t => t.Project).WithMany().HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.ProjectId, t.Name }).IsUnique();
        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
