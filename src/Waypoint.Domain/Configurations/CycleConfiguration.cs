using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class CycleConfiguration : IEntityTypeConfiguration<Cycle>
{
    public void Configure(EntityTypeBuilder<Cycle> builder)
    {
        builder.ToTable("cycles");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.State).HasMaxLength(20);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(c => c.Project).WithMany().HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.ProjectId, c.Name }).IsUnique();
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
