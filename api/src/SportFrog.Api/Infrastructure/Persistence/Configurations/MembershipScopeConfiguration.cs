using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="MembershipScope"/> onto the <c>membership_scopes</c>
/// table.
///
/// This entity carries org_id, so every query over it is already subject to
/// the isolation policy and returns nothing without an established
/// organization context — same note as
/// <see cref="OrganizationMembershipConfiguration"/>.
/// </summary>
internal sealed class MembershipScopeConfiguration : IEntityTypeConfiguration<MembershipScope>
{
    public void Configure(EntityTypeBuilder<MembershipScope> builder)
    {
        builder.ToTable("membership_scopes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.MembershipId).HasColumnName("membership_id");

        // Maps onto the PostgreSQL membership_scope_type enum, not to text.
        builder.Property(x => x.ScopeType).HasColumnName("scope_type");

        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.ClubId).HasColumnName("club_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Mirrors the database's two partial unique indexes
        // (uq_membership_scope_competition / uq_membership_scope_club):
        // declared for EF's own metadata and for anyone reading the model,
        // even though the migration's SQL is what actually enforces them.
        builder.HasIndex(x => new { x.MembershipId, x.CompetitionId })
            .IsUnique()
            .HasFilter("scope_type = 'competition'")
            .HasDatabaseName("uq_membership_scope_competition");
        builder.HasIndex(x => new { x.MembershipId, x.ClubId })
            .IsUnique()
            .HasFilter("scope_type = 'club'")
            .HasDatabaseName("uq_membership_scope_club");

        builder.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId);
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId);
    }
}
