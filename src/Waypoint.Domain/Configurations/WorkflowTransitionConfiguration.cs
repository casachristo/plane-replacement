using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("workflow_transitions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(t => t.Workflow).WithMany().HasForeignKey(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.FromState).WithMany().HasForeignKey(t => t.FromStateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToState).WithMany().HasForeignKey(t => t.ToStateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.WorkflowId, t.FromStateId, t.ToStateId }).IsUnique();
    }
}
