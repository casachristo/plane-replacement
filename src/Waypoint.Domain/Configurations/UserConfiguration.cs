using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.OidcSub).HasMaxLength(200);
        builder.Property(u => u.OidcIssuer).HasMaxLength(500);
        builder.Property(u => u.Groups).HasColumnType("text[]");
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => new { u.OidcIssuer, u.OidcSub }).IsUnique()
            .HasFilter("oidc_sub IS NOT NULL");
        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
