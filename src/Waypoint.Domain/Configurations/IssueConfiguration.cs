using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.Title).HasMaxLength(500).IsRequired();
        builder.Property(i => i.DescriptionMd).IsRequired();
        builder.Property(i => i.Priority).HasConversion<int>();
        builder.Property(i => i.Category).HasConversion<int>();
        builder.Property(i => i.AssigneeIds).HasColumnType("uuid[]").HasDefaultValueSql("'{}'::uuid[]");
        builder.Property(i => i.ExternalId).HasMaxLength(200);
        builder.Property(i => i.ExternalSource).HasMaxLength(50);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(i => i.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(i => i.Project).WithMany().HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.State).WithMany().HasForeignKey(i => i.StateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.IssueType).WithMany().HasForeignKey(i => i.IssueTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ParentIssue).WithMany().HasForeignKey(i => i.ParentIssueId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Epic).WithMany().HasForeignKey(i => i.EpicId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.Cycle).WithMany().HasForeignKey(i => i.CycleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(i => new { i.ProjectId, i.SequenceId }).IsUnique();
        builder.HasIndex(i => new { i.ProjectId, i.StateId });
        builder.HasIndex(i => i.UpdatedAt);
        builder.HasQueryFilter(i => i.DeletedAt == null);
    }
}
