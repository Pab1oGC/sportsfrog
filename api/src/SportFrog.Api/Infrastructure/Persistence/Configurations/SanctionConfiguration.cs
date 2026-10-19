using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Sanction"/> onto the <c>sanctions</c> table.</summary>
internal sealed class SanctionConfiguration : IEntityTypeConfiguration<Sanction>
{
    public void Configure(EntityTypeBuilder<Sanction> builder)
    {
        builder.ToTable("sanctions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.AthleteId).HasColumnName("athlete_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.MatchId).HasColumnName("match_id");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.CompletionRule).HasColumnName("completion_rule");
        builder.Property(x => x.MatchesRequired).HasColumnName("matches_required");
        builder.Property(x => x.FineAmount).HasColumnName("fine_amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.FineWaivedAt).HasColumnName("fine_waived_at");
        builder.Property(x => x.FineWaiverReason).HasColumnName("fine_waiver_reason");
        builder.Property(x => x.CredentialWithheldAt).HasColumnName("credential_withheld_at");
        builder.Property(x => x.ImposedBy).HasColumnName("imposed_by");
        builder.Property(x => x.ImposedAt).HasColumnName("imposed_at").HasDefaultValueSql("now()");
        builder.Property(x => x.LiftedAt).HasColumnName("lifted_at");
        builder.Property(x => x.LiftReason).HasColumnName("lift_reason");
        builder.Property(x => x.VoidedAt).HasColumnName("voided_at");
        builder.Property(x => x.VoidReason).HasColumnName("void_reason");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // No query filter, unlike RosterEntry or TeamStaff: a sanction is a
        // historical record of what was decided and why, the same standing
        // IssuedDocument and TuitionCharge have — it stays visible even once
        // the team or the competition it names is archived, rather than
        // disappearing along with them.
        builder.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
