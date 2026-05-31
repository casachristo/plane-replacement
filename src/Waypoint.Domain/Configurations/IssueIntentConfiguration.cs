using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class IssueIntentConfiguration : IEntityTypeConfiguration<IssueIntent>
{
    public void Configure(EntityTypeBuilder<IssueIntent> builder)
    {
        builder.ToTable("issue_intents");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.ModulePath).HasMaxLength(500).IsRequired();
        builder.Property(i => i.IntentText).IsRequired();
        builder.Property(i => i.LockAcquiredAt).HasDefaultValueSql("now()");
        builder.HasOne(i => i.Project).WithMany().HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.DeclaredByToken).WithMany().HasForeignKey(i => i.DeclaredByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.LinkedIssue).WithMany().HasForeignKey(i => i.LinkedIssueId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(i => new { i.ProjectId, i.ModulePath });
        builder.HasIndex(i => i.LockAcquiredAt);
    }
}
