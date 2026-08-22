using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="OrganizationMembership"/> onto the
/// <c>organization_memberships</c> table.
///
/// This entity carries org_id, so every query over it is already subject to
/// the isolation policy and returns nothing without an established
/// organization context.
/// </summary>
internal sealed class OrganizationMembershipConfiguration
    : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");

        // Maps onto the PostgreSQL membership_role enum, not to text.
        builder.Property(x => x.Role).HasColumnName("role");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
