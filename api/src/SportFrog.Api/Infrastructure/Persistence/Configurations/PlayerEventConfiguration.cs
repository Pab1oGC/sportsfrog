using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PlayerEvent"/> onto the <c>player_events</c> table.</summary>
internal sealed class PlayerEventConfiguration : IEntityTypeConfiguration<PlayerEvent>
{
    public void Configure(EntityTypeBuilder<PlayerEvent> builder)
    {
        builder.ToTable("player_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.RosterEntryId).HasColumnName("roster_entry_id");
        builder.Property(x => x.MetricId).HasColumnName("metric_id");
        builder.Property(x => x.PeriodNumber).HasColumnName("period_number");
        builder.Property(x => x.Minute).HasColumnName("minute");
        builder.Property(x => x.Quantity).HasColumnName("quantity");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Same reasoning as Match.UseXminAsConcurrencyToken(): correcting or
        // deleting an event someone else just corrected or deleted should
        // fail loudly (409), not overwrite the newer row or silently no-op.
        // A concurrent delete is not a special case this needs to reason
        // about separately -- if the row is gone by the time this saves, EF
        // throws the same DbUpdateConcurrencyException (zero rows matched),
        // and the handler reports it the same way. A row that never existed
        // in the first place is caught earlier, by the ordinary
        // load-or-NotFound check every handler already does.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Visible while the match it belongs to is. The table has no
        // deleted_at of its own, so this is entirely a fact about the fixture
        // — a withdrawn competition takes its statistics with it, and a
        // fixture struck from the calendar takes the events somebody entered
        // against it before noticing the mistake.
        builder.HasQueryFilter(x =>
            x.Match!.DeletedAt == null && x.Match.Competition!.DeletedAt == null);

        builder.HasOne(x => x.Match)
            .WithMany()
            .HasForeignKey(x => x.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RosterEntry)
            .WithMany()
            .HasForeignKey(x => x.RosterEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Metric).WithMany().HasForeignKey(x => x.MetricId);
    }
}
