using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class TokenAuditLogConfiguration : IEntityTypeConfiguration<TokenAuditLog>
{
    public void Configure(EntityTypeBuilder<TokenAuditLog> builder)
    {
        builder.ToTable("token_audit_log");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.PassthroughActorId).HasMaxLength(200);
        builder.Property(a => a.PassthroughActorLabel).HasMaxLength(200);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Path).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Method).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Ip).HasMaxLength(64);
        builder.Property(a => a.At).HasDefaultValueSql("now()");
        builder.HasOne(a => a.Token).WithMany().HasForeignKey(a => a.TokenId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.TokenId, a.At });
    }
}
