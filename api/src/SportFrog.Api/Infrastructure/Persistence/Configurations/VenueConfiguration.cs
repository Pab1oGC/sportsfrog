using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Venue"/> onto the <c>venues</c> table.</summary>
internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.MapsUrl).HasColumnName("maps_url");
        builder.Property(x => x.IsActive).HasColumnName("is_active");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // No filter on this one, unlike clubs and athletes. Their indexes are
        // partial because a soft-deleted row keeps sitting in the table and
        // would hold its name forever; here removal is physical, so a name is
        // free the moment the venue is gone and there is nothing to exclude.
        builder.HasIndex(x => new { x.OrgId, x.Name }).IsUnique();

        // Cascade, matching the schema: removing a venue removes its spaces.
        // That is the behaviour the delete endpoint has to protect, not one it
        // relies on.
        builder.HasMany(x => x.Spaces)
            .WithOne(space => space.Venue)
            .HasForeignKey(space => space.VenueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
