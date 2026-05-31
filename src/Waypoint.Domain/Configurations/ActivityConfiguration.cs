using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activity");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.ActorType).HasConversion<int>();
        builder.Property(a => a.ActorLabel).HasMaxLength(200);
        builder.Property(a => a.Verb).HasMaxLength(100).IsRequired();
        builder.Property(a => a.BeforeJson).HasColumnType("jsonb");
        builder.Property(a => a.AfterJson).HasColumnType("jsonb");
        builder.Property(a => a.At).HasDefaultValueSql("now()");
        builder.HasOne(a => a.Issue).WithMany().HasForeignKey(a => a.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.IssueId, a.At });
    }
}
