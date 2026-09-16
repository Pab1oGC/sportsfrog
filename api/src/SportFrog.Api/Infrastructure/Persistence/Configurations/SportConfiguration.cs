using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Sport"/> onto the <c>sports</c> table.</summary>
internal sealed class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    /// <summary>
    /// Written out rather than derived from the enum's name, so the strings
    /// the CHECK constraint accepts appear here literally. A rename of an enum
    /// member is then a compile-time concern and not a runtime surprise.
    /// </summary>
    // Nested conditionals rather than a switch expression: EF compiles this
    // lambda into a SQL expression tree, and a switch expression cannot
    // appear inside one.
    private static readonly ValueConverter<ScoreMode, string> ScoreModeConverter = new(
        mode => mode == ScoreMode.Sets ? "sets" : mode == ScoreMode.Judged ? "judged" : "cumulative",
        stored => stored == "sets" ? ScoreMode.Sets : stored == "judged" ? ScoreMode.Judged : ScoreMode.Cumulative);

    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("sports");

        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.PeriodLabel).HasColumnName("period_label").IsRequired();
        builder.Property(x => x.DefaultPeriods).HasColumnName("default_periods");
        builder.Property(x => x.PeriodHasClock).HasColumnName("period_has_clock");
        builder.Property(x => x.DefaultMinutes).HasColumnName("default_minutes");
        builder.Property(x => x.DefaultBreakMinutes).HasColumnName("default_break_minutes");
        builder.Property(x => x.ScoringUnit).HasColumnName("scoring_unit").IsRequired();

        builder.Property(x => x.ScoreMode)
            .HasColumnName("score_mode")
            .HasConversion(ScoreModeConverter)
            .IsRequired();

        builder.Property(x => x.IsIndividual).HasColumnName("is_individual");
        builder.Property(x => x.MaxEntrySize).HasColumnName("max_entry_size");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasMany(x => x.Metrics)
            .WithOne(metric => metric.Sport)
            .HasForeignKey(metric => metric.SportCode);
    }
}
