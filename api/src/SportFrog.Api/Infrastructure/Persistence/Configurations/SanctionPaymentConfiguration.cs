using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SanctionPayment"/> onto the <c>sanction_payments</c> table.</summary>
internal sealed class SanctionPaymentConfiguration : IEntityTypeConfiguration<SanctionPayment>
{
    public void Configure(EntityTypeBuilder<SanctionPayment> builder)
    {
        builder.ToTable("sanction_payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.SanctionId).HasColumnName("sanction_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.Method).HasColumnName("method");
        builder.Property(x => x.ReceiptKey).HasColumnName("receipt_key");
        builder.Property(x => x.ReviewState).HasColumnName("review_state");
        builder.Property(x => x.ReviewReason).HasColumnName("review_reason");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.VoidedAt).HasColumnName("voided_at");
        builder.Property(x => x.VoidReason).HasColumnName("void_reason");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => x.SanctionId);

        builder.HasOne(x => x.Sanction)
            .WithMany()
            .HasForeignKey(x => x.SanctionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
