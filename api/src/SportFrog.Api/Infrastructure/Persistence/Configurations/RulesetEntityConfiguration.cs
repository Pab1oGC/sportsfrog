using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Ruleset"/> onto the <c>rulesets</c> table.</summary>
internal sealed class RulesetEntityConfiguration : IEntityTypeConfiguration<Ruleset>
{
    /// <summary>
    /// Snake case, so what is stored reads the way the column's documentation
    /// says it does. The jsonb is read by more than this application — the
    /// public path and the reporting queries reach into it with SQL — and a
    /// key named winnerScore in the database because C# spells it that way
    /// would be this layer leaking into everyone else's.
    /// </summary>
    private static readonly JsonSerializerOptions StorageFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

        // An absent rule and a rule set to null are the same statement, and
        // the shorter one is easier to read in psql.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Configure(EntityTypeBuilder<Ruleset> builder)
    {
        builder.ToTable("rulesets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.SportCode).HasColumnName("sport_code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();

        builder.Property(x => x.Config)
            .HasColumnName("config")
            .HasColumnType("jsonb")
            .HasConversion(
                config => JsonSerializer.Serialize(config, StorageFormat),
                stored => JsonSerializer.Deserialize<RulesetConfiguration>(stored, StorageFormat)!,
                ConfigComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.Name }).IsUnique();

        builder.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportCode);
    }

    /// <summary>
    /// Tells the change tracker how to tell one configuration from another.
    /// </summary>
    /// <remarks>
    /// Required, and not a formality. Without it the tracker compares
    /// converted values by reference, and a record holding lists and a
    /// dictionary is never equal to the copy it made of itself: every save
    /// would rewrite the column and the audit log would report a change to a
    /// ruleset nobody edited. Comparing the stored text is exact, because the
    /// stored text is what the column holds.
    ///
    /// The snapshot is a real copy for the same reason. Handed the same
    /// instance, the tracker would compare an edited configuration against
    /// itself and find nothing changed.
    /// </remarks>
    private static readonly ValueComparer<RulesetConfiguration> ConfigComparer = new(
        (left, right) =>
            JsonSerializer.Serialize(left, StorageFormat)
            == JsonSerializer.Serialize(right, StorageFormat),
        config => JsonSerializer.Serialize(config, StorageFormat).GetHashCode(),
        config => JsonSerializer.Deserialize<RulesetConfiguration>(
            JsonSerializer.Serialize(config, StorageFormat), StorageFormat)!);
}
