using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CategoryStage"/> onto the <c>category_stages</c> table.</summary>
internal sealed class CategoryStageConfiguration : IEntityTypeConfiguration<CategoryStage>
{
    public void Configure(EntityTypeBuilder<CategoryStage> builder)
    {
        builder.ToTable("category_stages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.RulesetId).HasColumnName("ruleset_id");

        // Same conversion as CompetitionStage.Kind — see
        // CompetitionStageConfiguration.KindConverter, shared rather than
        // repeated so the two tables cannot drift into spelling a stage
        // differently.
        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion(CompetitionStageConfiguration.KindConverter)
            .IsRequired();

        builder.Property(x => x.QualifiersPerGroup).HasColumnName("qualifiers_per_group");
        builder.Property(x => x.KnockoutEntrants).HasColumnName("knockout_entrants");
        builder.Property(x => x.UsesRepechage).HasColumnName("uses_repechage");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.CategoryId, x.Kind }).IsUnique();

        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        builder.HasOne(x => x.Ruleset).WithMany().HasForeignKey(x => x.RulesetId);
    }
}
