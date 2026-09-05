using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Club"/> onto the <c>clubs</c> table.</summary>
internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("clubs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.ShortName).HasColumnName("short_name");
        builder.Property(x => x.LogoUrl).HasColumnName("logo_url");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.IsUnaffiliated).HasColumnName("is_unaffiliated");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Unique among the living, matching the partial index: a name freed by
        // a logical deletion can be taken again.
        builder.HasIndex(x => new { x.OrgId, x.Name })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
