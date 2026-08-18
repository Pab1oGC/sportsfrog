using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="RefreshToken"/> onto the <c>refresh_tokens</c> table.</summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Presenting a token resolves it by its hash, so the lookup has to
        // be indexed and unique.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
