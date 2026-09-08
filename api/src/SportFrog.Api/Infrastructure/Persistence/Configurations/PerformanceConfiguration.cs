using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Performance"/> onto the <c>performances</c> table.</summary>
internal sealed class PerformanceConfiguration : IEntityTypeConfiguration<Performance>
{
    public void Configure(EntityTypeBuilder<Performance> builder)
    {
        builder.ToTable("performances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Score).HasColumnName("score");
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        builder.Property(x => x.ModifiedBy).HasColumnName("modified_by");
        builder.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // One performance slot per team per category, among the living —
        // matching uq_performances_team_category.
        builder.HasIndex(x => new { x.CategoryId, x.TeamId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // Visible while the row and the competition above it are, same as
        // Match — a withdrawn competition takes its classification stage
        // with it.
        builder.HasQueryFilter(x =>
            x.DeletedAt == null && x.Competition!.DeletedAt == null);

        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
