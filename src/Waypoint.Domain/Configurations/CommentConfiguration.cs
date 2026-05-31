using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.BodyMd).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(c => c.Issue).WithMany().HasForeignKey(c => c.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.AuthorUser).WithMany().HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(c => c.ParentComment).WithMany().HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.IssueId, c.CreatedAt });
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
