using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Organization"/> onto the <c>organizations</c> table.
///
/// Table and column names are stated one by one rather than derived from a
/// naming convention: the schema is defined by the migrations' SQL, so this
/// mapping is the explicit point of contact between two separate sources of
/// truth and has to be readable as such.
/// </summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.LogoUrl).HasColumnName("logo_url");
        builder.Property(x => x.Plan).HasColumnName("plan").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        // citext: the public URL resolves the organization regardless of casing.
        builder.Property(x => x.Slug).HasColumnName("slug").HasColumnType("citext").IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        // Owned by the database: a default on insert, the touch_updated_at
        // trigger on update. EF reads them back and never writes them.
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasMany(x => x.Memberships)
            .WithOne(x => x.Organization)
            .HasForeignKey(x => x.OrgId)
            .OnDelete(DeleteBehavior.Cascade);

        // Business visibility, not security: the isolation policy lives in
        // the database, this filter only hides soft-deleted rows.
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
