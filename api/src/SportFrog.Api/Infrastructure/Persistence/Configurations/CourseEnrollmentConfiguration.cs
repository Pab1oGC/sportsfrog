using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CourseEnrollment"/> onto the <c>course_enrollments</c> table.</summary>
internal sealed class CourseEnrollmentConfiguration : IEntityTypeConfiguration<CourseEnrollment>
{
    public void Configure(EntityTypeBuilder<CourseEnrollment> builder)
    {
        builder.ToTable("course_enrollments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CourseId).HasColumnName("course_id");
        builder.Property(x => x.AthleteId).HasColumnName("athlete_id");
        builder.Property(x => x.EnrolledAt).HasColumnName("enrolled_at").HasDefaultValueSql("now()");
        builder.Property(x => x.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(x => x.FeeOverrideBob).HasColumnName("fee_override").HasColumnType("numeric(12,2)");
        builder.Property(x => x.FeeOverrideReason).HasColumnName("fee_override_reason");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Partial like uq_roster_team_athlete: excludes deleted_at only, not
        // withdrawn_at — a withdrawn enrollment still blocks a fresh one,
        // see EnrollmentPolicy.
        builder.HasIndex(x => new { x.CourseId, x.AthleteId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Athlete)
            .WithMany()
            .HasForeignKey(x => x.AthleteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
