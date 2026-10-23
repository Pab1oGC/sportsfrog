using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CompetitionStage"/> onto the <c>competition_stages</c> table.</summary>
internal sealed class CompetitionStageConfiguration : IEntityTypeConfiguration<CompetitionStage>
{
    /// <summary>
    /// Written out rather than derived from the enum's name, the same choice
    /// <see cref="SportConfiguration"/>'s own <c>ScoreModeConverter</c> makes
    /// and for the same reason: the strings a reader of the column sees
    /// appear here literally, not implicitly through whatever a C# member
    /// happens to be called today.
    /// </summary>
    // Nested conditionals rather than a switch expression, same reason as
    // ScoreModeConverter: EF compiles this lambda into a SQL expression
    // tree, and a switch expression cannot appear inside one.
    internal static readonly ValueConverter<StageKind, string> KindConverter = new(
        kind => kind == StageKind.League ? "league"
            : kind == StageKind.Groups ? "groups"
            : kind == StageKind.Classification ? "classification"
            : "bracket",
        stored => stored == "league" ? StageKind.League
            : stored == "groups" ? StageKind.Groups
            : stored == "classification" ? StageKind.Classification
            : StageKind.Bracket);

    public void Configure(EntityTypeBuilder<CompetitionStage> builder)
    {
        builder.ToTable("competition_stages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion(KindConverter)
            .IsRequired();

        builder.Property(x => x.BufferMinutes).HasColumnName("buffer_minutes");
        builder.Property(x => x.SpaceIds).HasColumnName("space_ids");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.CompetitionId, x.Kind }).IsUnique();

        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
    }
}
