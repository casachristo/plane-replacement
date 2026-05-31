using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("states");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Color).HasMaxLength(7).IsRequired();
        builder.Property(s => s.Group).HasConversion<int>();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(s => s.Project).WithMany().HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.ProjectId, s.Name }).IsUnique();
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}
