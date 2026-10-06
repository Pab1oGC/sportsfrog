using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CredentialDesign"/> onto the <c>credential_designs</c> table.</summary>
internal sealed class CredentialDesignConfiguration : IEntityTypeConfiguration<CredentialDesign>
{
    public void Configure(EntityTypeBuilder<CredentialDesign> builder)
    {
        builder.ToTable("credential_designs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.LegalText).HasColumnName("legal_text");
        builder.Property(x => x.BackgroundKey).HasColumnName("background_key");
        builder.Property(x => x.AccentColorHex).HasColumnName("accent_color_hex");
        builder.Property(x => x.LogoKey).HasColumnName("logo_key");
        builder.Property(x => x.IsDefault).HasColumnName("is_default");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();
    }
}
