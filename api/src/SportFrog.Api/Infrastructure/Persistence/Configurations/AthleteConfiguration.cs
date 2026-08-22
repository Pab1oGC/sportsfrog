using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Athlete"/> onto the <c>athletes</c> table.</summary>
internal sealed class AthleteConfiguration : IEntityTypeConfiguration<Athlete>
{
    public void Configure(EntityTypeBuilder<Athlete> builder)
    {
        builder.ToTable("athletes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(x => x.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(x => x.BirthDate).HasColumnName("birth_date");
        builder.Property(x => x.Gender).HasColumnName("gender");
        builder.Property(x => x.PhotoUrl).HasColumnName("photo_url");
        builder.Property(x => x.GuardianName).HasColumnName("guardian_name");
        builder.Property(x => x.GuardianPhone).HasColumnName("guardian_phone");
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

        // The constraint RF-43 rests on: one document, one person, within the
        // organization.
        builder.HasIndex(x => new { x.OrgId, x.DocumentId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        builder.HasIndex(x => new { x.OrgId, x.LastName, x.FirstName })
            .HasFilter("deleted_at IS NULL");

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
