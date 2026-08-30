using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Team"/> onto the <c>teams</c> table.</summary>
internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.ClubId).HasColumnName("club_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.GroupLabel).HasColumnName("group_label");
        builder.Property(x => x.Seed).HasColumnName("seed");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // One entry per club per category, among the living: a club that
        // withdrew and is re-entered takes the place it left.
        builder.HasIndex(x => new { x.CategoryId, x.ClubId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // Two conditions, and both are the team's own visibility: its row is
        // not deleted, and the competition it plays in is still there. The
        // second reaches through the category because that is where the link
        // to the competition lives — a withdrawn competition takes its
        // categories with it, and it has to take their teams too.
        builder.HasQueryFilter(x =>
            x.DeletedAt == null && x.Category!.Competition!.DeletedAt == null);

        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
    }
}
