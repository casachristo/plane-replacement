using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("api_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Prefix).HasMaxLength(16).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(t => t.Scopes).HasColumnType("text[]").IsRequired();
        builder.Property(t => t.Kind).HasConversion<int>();
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(t => t.Prefix);
    }
}
