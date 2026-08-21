using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="VenueSpace"/> onto the <c>venue_spaces</c> table.</summary>
internal sealed class VenueSpaceConfiguration : IEntityTypeConfiguration<VenueSpace>
{
    public void Configure(EntityTypeBuilder<VenueSpace> builder)
    {
        builder.ToTable("venue_spaces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.VenueId).HasColumnName("venue_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Within its venue, not within the organization: two grounds each
        // calling their main pitch "Cancha 1" is normal, and forcing them
        // apart would make organizers invent names nobody uses.
        builder.HasIndex(x => new { x.VenueId, x.Name }).IsUnique();
    }
}
