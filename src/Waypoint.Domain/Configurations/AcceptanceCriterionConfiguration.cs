using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class AcceptanceCriterionConfiguration : IEntityTypeConfiguration<AcceptanceCriterion>
{
    public void Configure(EntityTypeBuilder<AcceptanceCriterion> builder)
    {
        builder.ToTable("acceptance_criteria");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Text).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.CheckedByActorType).HasConversion<int>();
        builder.Property(a => a.CheckedByActorLabel).HasMaxLength(200);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(a => a.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(a => a.Issue).WithMany().HasForeignKey(a => a.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.IssueId, a.Position });
        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
