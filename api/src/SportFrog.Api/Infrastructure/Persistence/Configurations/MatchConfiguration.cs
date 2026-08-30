using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Match"/> onto the <c>matches</c> table.</summary>
internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    /// <summary>
    /// The period scores carry their own short property names, so no naming
    /// policy applies here — the shape is decided on the record itself and
    /// this only has to not interfere with it.
    /// </summary>
    private static readonly JsonSerializerOptions StorageFormat = new();

    /// <summary>
    /// Without this the change tracker compares the list by reference and
    /// finds every saved match different from itself, rewriting the column
    /// and filling the audit log with scores nobody touched.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<PeriodScore>?> ScoresComparer = new(
        (left, right) =>
            JsonSerializer.Serialize(left, StorageFormat)
            == JsonSerializer.Serialize(right, StorageFormat),
        scores => JsonSerializer.Serialize(scores, StorageFormat).GetHashCode(),
        scores => JsonSerializer.Deserialize<IReadOnlyList<PeriodScore>>(
            JsonSerializer.Serialize(scores, StorageFormat), StorageFormat)!);

    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.HomeTeamId).HasColumnName("home_team_id");
        builder.Property(x => x.AwayTeamId).HasColumnName("away_team_id");
        builder.Property(x => x.VenueSpaceId).HasColumnName("venue_space_id");
        builder.Property(x => x.RoundNumber).HasColumnName("round_number");
        builder.Property(x => x.Phase).HasColumnName("phase");
        builder.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.WalkoverTeamId).HasColumnName("walkover_team_id");
        builder.Property(x => x.HomeTotal).HasColumnName("home_total");
        builder.Property(x => x.AwayTotal).HasColumnName("away_total");
        builder.Property(x => x.PenaltyHomeScore).HasColumnName("penalty_home_score");
        builder.Property(x => x.PenaltyAwayScore).HasColumnName("penalty_away_score");
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        builder.Property(x => x.ModifiedBy).HasColumnName("modified_by");
        builder.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.PeriodScores)
            .HasColumnName("period_scores")
            .HasColumnType("jsonb")
            .HasConversion(
                scores => JsonSerializer.Serialize(scores, StorageFormat),
                stored => JsonSerializer.Deserialize<IReadOnlyList<PeriodScore>>(
                    stored, StorageFormat)!,
                ScoresComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // One live fixture per space and moment, matching uq_space_schedule.
        // Declared here as well as in the schema so the model knows a write
        // can fail on it, and because a reader of this file should not have to
        // go and find out that two matches cannot share a pitch.
        //
        // The predicate leaves out what does not occupy the ground: a
        // cancelled or postponed fixture frees its slot, and so does a deleted
        // one.
        builder.HasIndex(x => new { x.VenueSpaceId, x.ScheduledAt })
            .IsUnique()
            .HasFilter(
                "venue_space_id IS NOT NULL AND scheduled_at IS NOT NULL "
                + "AND deleted_at IS NULL AND status NOT IN ('cancelled','postponed')");

        // Visible while the fixture and the competition above it are. Read
        // through the competition rather than copied down, so withdrawing one
        // takes its calendar with it.
        builder.HasQueryFilter(x =>
            x.DeletedAt == null && x.Competition!.DeletedAt == null);

        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        builder.HasOne(x => x.VenueSpace).WithMany().HasForeignKey(x => x.VenueSpaceId);

        // Both sides point at the same table, so the foreign keys have to be
        // told apart explicitly or EF invents a single one and the model stops
        // matching the schema.
        builder.HasOne(x => x.HomeTeam)
            .WithMany()
            .HasForeignKey(x => x.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AwayTeam)
            .WithMany()
            .HasForeignKey(x => x.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
