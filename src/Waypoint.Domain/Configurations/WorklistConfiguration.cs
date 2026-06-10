using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Configurations;

public class WorklistConfiguration : IEntityTypeConfiguration<Worklist>
{
    private static readonly JsonSerializerOptions Json = new();

    public void Configure(EntityTypeBuilder<Worklist> builder)
    {
        builder.ToTable("worklists");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");

        // One worklist per project — the singleton invariant lives in the schema.
        builder.HasOne(w => w.Project).WithMany().HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(w => w.ProjectId).IsUnique();

        builder.Property(w => w.State)
            .HasConversion(new EnumToStringConverter<WorklistState>())
            .HasMaxLength(20)
            .IsRequired();

        var guidList = new ValueConverter<List<Guid>, string>(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<List<Guid>>(v, Json) ?? new());
        var guidListComparer = new ValueComparer<List<Guid>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g.GetHashCode())),
            v => v.ToList());
        builder.Property(w => w.OrderedIds).HasConversion(guidList).HasColumnType("jsonb")
            .Metadata.SetValueComparer(guidListComparer);

        var skipList = new ValueConverter<List<WorklistSkip>, string>(
            v => JsonSerializer.Serialize(v, Json),
            v => JsonSerializer.Deserialize<List<WorklistSkip>>(v, Json) ?? new());
        var skipListComparer = new ValueComparer<List<WorklistSkip>>(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(),
            v => JsonSerializer.Deserialize<List<WorklistSkip>>(JsonSerializer.Serialize(v, Json), Json) ?? new());
        builder.Property(w => w.Skipped).HasConversion(skipList).HasColumnType("jsonb")
            .Metadata.SetValueComparer(skipListComparer);

        builder.Property(w => w.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(w => w.UpdatedAt).HasDefaultValueSql("now()");

        builder.Ignore(w => w.CurrentId);
        builder.Ignore(w => w.RemainingCount);
        builder.Ignore(w => w.SkippedCount);
    }
}
