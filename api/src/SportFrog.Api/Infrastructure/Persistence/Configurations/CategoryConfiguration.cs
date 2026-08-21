using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Category"/> onto the <c>categories</c> table.</summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.RulesetId).HasColumnName("ruleset_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Gender).HasColumnName("gender");
        builder.Property(x => x.BirthDateFrom).HasColumnName("birth_date_from");
        builder.Property(x => x.BirthDateTo).HasColumnName("birth_date_to");
        builder.Property(x => x.MaxRosterSize).HasColumnName("max_roster_size");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Total, not partial: the table carries no deleted_at, so there are no
        // rows hiding behind a visibility filter for a name to be freed by.
        builder.HasIndex(x => new { x.CompetitionId, x.Name }).IsUnique();

        // A category of a withdrawn competition is not visible either. The
        // filter reaches through the navigation because the visibility lives
        // on the parent: a category has no deleted_at of its own, and its
        // existence is entirely a fact about the competition it divides.
        builder.HasQueryFilter(x => x.Competition!.DeletedAt == null);

        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
        builder.HasOne(x => x.Ruleset).WithMany().HasForeignKey(x => x.RulesetId);
    }
}
