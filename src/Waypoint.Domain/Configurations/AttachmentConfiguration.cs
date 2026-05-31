using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Filename).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Mime).HasMaxLength(200).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(a => a.Issue).WithMany().HasForeignKey(a => a.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Comment).WithMany().HasForeignKey(a => a.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.UploadedByUser).WithMany().HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => a.IssueId);
        builder.HasIndex(a => a.CommentId);
        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
