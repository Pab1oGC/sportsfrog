using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CourseSession"/> onto the <c>course_sessions</c> table.</summary>
internal sealed class CourseSessionConfiguration : IEntityTypeConfiguration<CourseSession>
{
    public void Configure(EntityTypeBuilder<CourseSession> builder)
    {
        builder.ToTable("course_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CourseId).HasColumnName("course_id");
        builder.Property(x => x.SessionDate).HasColumnName("session_date");
        builder.Property(x => x.StartsAt).HasColumnName("starts_at");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at");

        // Maps onto the PostgreSQL course_session_state enum, not to text.
        builder.Property(x => x.Status).HasColumnName("status");

        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Mirrors uq_course_sessions_course_date_time: declared for EF's
        // own metadata, even though the migration's SQL is what enforces
        // it. Includes StartsAt, not just the date — see that index's own
        // comment for why a course meeting twice in one day needs it.
        builder.HasIndex(x => new { x.CourseId, x.SessionDate, x.StartsAt })
            .IsUnique()
            .HasDatabaseName("uq_course_sessions_course_date_time");

        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
