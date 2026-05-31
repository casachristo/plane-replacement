using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Color).HasMaxLength(7).IsRequired();
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(l => l.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(l => l.Project).WithMany().HasForeignKey(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.ParentLabel).WithMany().HasForeignKey(l => l.ParentLabelId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique();
        builder.HasQueryFilter(l => l.DeletedAt == null);
    }
}

public class IssueLabelConfiguration : IEntityTypeConfiguration<IssueLabel>
{
    public void Configure(EntityTypeBuilder<IssueLabel> builder)
    {
        builder.ToTable("issue_labels");
        builder.HasKey(il => new { il.IssueId, il.LabelId });
        builder.HasOne(il => il.Issue).WithMany().HasForeignKey(il => il.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(il => il.Label).WithMany().HasForeignKey(il => il.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
