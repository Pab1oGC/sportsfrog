using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="TuitionCharge"/> onto the <c>tuition_charges</c> table.</summary>
internal sealed class TuitionChargeConfiguration : IEntityTypeConfiguration<TuitionCharge>
{
    public void Configure(EntityTypeBuilder<TuitionCharge> builder)
    {
        builder.ToTable("tuition_charges");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.EnrollmentId).HasColumnName("enrollment_id");
        builder.Property(x => x.Period).HasColumnName("period");
        builder.Property(x => x.AmountBob).HasColumnName("amount").HasColumnType("numeric(12,2)");
        builder.Property(x => x.DueOn).HasColumnName("due_on");
        builder.Property(x => x.WaivedAt).HasColumnName("waived_at");
        builder.Property(x => x.WaiverReason).HasColumnName("waiver_reason");
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

        // Mirrors uq_tuition_charges_enrollment_period: declared for EF's
        // own metadata, even though the migration's SQL is what enforces
        // it.
        builder.HasIndex(x => new { x.EnrollmentId, x.Period }).IsUnique();

        builder.HasOne(x => x.Enrollment)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
