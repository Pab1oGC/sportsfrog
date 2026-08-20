using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SportMetric"/> onto the <c>sport_metrics</c> table.</summary>
internal sealed class SportMetricConfiguration : IEntityTypeConfiguration<SportMetric>
{
    public void Configure(EntityTypeBuilder<SportMetric> builder)
    {
        builder.ToTable("sport_metrics");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.SportCode).HasColumnName("sport_code").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").IsRequired();
        builder.Property(x => x.Label).HasColumnName("label").IsRequired();
        builder.Property(x => x.AffectsScore).HasColumnName("affects_score");
        builder.Property(x => x.IsRankable).HasColumnName("is_rankable");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.SportCode, x.Code }).IsUnique();
    }
}
