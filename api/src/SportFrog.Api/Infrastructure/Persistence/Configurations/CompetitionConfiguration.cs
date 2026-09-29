using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Competition"/> onto the <c>competitions</c> table.</summary>
internal sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    /// <summary>
    /// Snake case, matching the structure the column's own documentation
    /// describes. The settings are read by more than this application: the
    /// public path reaches into them to decide what a visitor is shown.
    /// </summary>
    private static readonly JsonSerializerOptions StorageFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Without this the change tracker compares the settings by reference and
    /// finds every saved competition different from itself, rewriting the
    /// column and filling the audit log with changes nobody made.
    /// </summary>
    private static readonly ValueComparer<CompetitionSettings> SettingsComparer = new(
        (left, right) =>
            JsonSerializer.Serialize(left, StorageFormat)
            == JsonSerializer.Serialize(right, StorageFormat),
        settings => JsonSerializer.Serialize(settings, StorageFormat).GetHashCode(),
        settings => JsonSerializer.Deserialize<CompetitionSettings>(
            JsonSerializer.Serialize(settings, StorageFormat), StorageFormat)!);

    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.ToTable("competitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.SportCode).HasColumnName("sport_code").IsRequired();
        builder.Property(x => x.RulesetId).HasColumnName("ruleset_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Season).HasColumnName("season").IsRequired();
        builder.Property(x => x.Format).HasColumnName("format").IsRequired();
        builder.Property(x => x.StartsOn).HasColumnName("starts_on");
        builder.Property(x => x.EndsOn).HasColumnName("ends_on");
        builder.Property(x => x.IsPublic).HasColumnName("is_public");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // citext: the column compares case-insensitively, so the type has to
        // be named or EF would generate a plain text parameter and the
        // comparison would depend on which side of the query it happened on.
        builder.Property(x => x.Slug).HasColumnName("slug").HasColumnType("citext").IsRequired();

        builder.Property(x => x.CaptureLevel).HasColumnName("capture_level");
        builder.Property(x => x.Status).HasColumnName("status");

        builder.Property(x => x.Settings)
            .HasColumnName("settings")
            .HasColumnType("jsonb")
            .HasConversion(
                settings => JsonSerializer.Serialize(settings, StorageFormat),
                stored => JsonSerializer.Deserialize<CompetitionSettings>(stored, StorageFormat)!,
                SettingsComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Unique among the living, matching the partial index: an address
        // freed by a logical deletion can be taken again.
        builder.HasIndex(x => new { x.OrgId, x.Slug })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // The name is unique among the living too, but case-insensitively --
        // uq_competitions_name is built on lower(name), an expression HasIndex
        // cannot describe, so it lives only in the migration. See
        // CompetitionUniqueness for the pre-check that agrees with it.

        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.HasOne(x => x.Sport).WithMany().HasForeignKey(x => x.SportCode);
        builder.HasOne(x => x.Ruleset).WithMany().HasForeignKey(x => x.RulesetId);
    }
}
