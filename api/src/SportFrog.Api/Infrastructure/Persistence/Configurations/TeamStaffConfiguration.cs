using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="TeamStaff"/> onto the <c>team_staff</c> table.</summary>
internal sealed class TeamStaffConfiguration : IEntityTypeConfiguration<TeamStaff>
{
    public void Configure(EntityTypeBuilder<TeamStaff> builder)
    {
        builder.ToTable("team_staff");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.AthleteId).HasColumnName("athlete_id");
        builder.Property(x => x.Role).HasColumnName("role");
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

        // One staff row per person per team, among the living — the same rule
        // roster_entries holds for a registration, and struck the same way: a
        // row struck as a mistake frees the person to be added again, a
        // withdrawal does not, because they genuinely worked here.
        builder.HasIndex(x => new { x.TeamId, x.AthleteId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // One head coach at a time. A team changing coach mid-season
        // withdraws the first and registers the second, which is what the
        // withdrawn_at half of this filter leaves room for.
        builder.HasIndex(x => x.TeamId)
            .IsUnique()
            .HasDatabaseName("uq_team_staff_head_coach")
            .HasFilter("role = 'head_coach' AND deleted_at IS NULL AND withdrawn_at IS NULL");

        // Visible while the row, its team and the competition above them all
        // are — read through the chain rather than copied down it, exactly as
        // RosterEntryConfiguration does, so a withdrawn competition takes its
        // staff with it without anything having to remember to mark them.
        builder.HasQueryFilter(x =>
            x.DeletedAt == null
            && x.Team!.DeletedAt == null
            && x.Team.Category!.Competition!.DeletedAt == null);

        // No inverse collection on Team: nothing reads a team's staff through
        // the team entity, and adding one would mean every existing read of a
        // Team carrying a second collection it never asked for.
        builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
        builder.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId);
    }
}
