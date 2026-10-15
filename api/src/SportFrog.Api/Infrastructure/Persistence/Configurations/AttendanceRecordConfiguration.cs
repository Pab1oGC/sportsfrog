using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AttendanceRecord"/> onto the <c>attendance_records</c> table.</summary>
internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.EnrollmentId).HasColumnName("enrollment_id");

        // Maps onto the PostgreSQL attendance_state enum, not to text.
        builder.Property(x => x.Status).HasColumnName("status");

        builder.Property(x => x.Note).HasColumnName("note");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Mirrors the database's own unique constraint on (session_id,
        // enrollment_id): declared for EF's own metadata, even though the
        // migration's SQL is what enforces it.
        builder.HasIndex(x => new { x.SessionId, x.EnrollmentId }).IsUnique();

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Enrollment)
            .WithMany()
            .HasForeignKey(x => x.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
