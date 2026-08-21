using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="RosterEntry"/> onto the <c>roster_entries</c> table.</summary>
internal sealed class RosterEntryConfiguration : IEntityTypeConfiguration<RosterEntry>
{
    public void Configure(EntityTypeBuilder<RosterEntry> builder)
    {
        builder.ToTable("roster_entries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.AthleteId).HasColumnName("athlete_id");
        builder.Property(x => x.JerseyNumber).HasColumnName("jersey_number");
        builder.Property(x => x.Position).HasColumnName("position");
        builder.Property(x => x.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.RegisteredAt)
            .HasColumnName("registered_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // One registration per person per team, among the living. A
        // registration struck as a mistake frees the person to be registered
        // again; a withdrawal does not, because they were genuinely on this
        // team and the record of that stays.
        builder.HasIndex(x => new { x.TeamId, x.AthleteId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // The shirt, unique only among players actually on the team: the
        // filter drops the withdrawn and the struck, so a number comes back
        // into circulation when its owner leaves.
        builder.HasIndex(x => new { x.TeamId, x.JerseyNumber })
            .IsUnique()
            .HasFilter("deleted_at IS NULL AND withdrawn_at IS NULL AND jersey_number IS NOT NULL");

        // Visible while the entry, its team and the competition above them all
        // are. Read through the chain rather than copied down it, so a
        // withdrawn competition takes its squads with it without anything
        // having to remember to mark them.
        builder.HasQueryFilter(x =>
            x.DeletedAt == null
            && x.Team!.DeletedAt == null
            && x.Team.Category!.Competition!.DeletedAt == null);

        builder.HasOne(x => x.Team).WithMany(team => team.Roster).HasForeignKey(x => x.TeamId);
        builder.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId);
    }
}
