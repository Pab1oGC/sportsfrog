using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CourseSchedule"/> onto the <c>course_schedules</c> table.</summary>
internal sealed class CourseScheduleConfiguration : IEntityTypeConfiguration<CourseSchedule>
{
    public void Configure(EntityTypeBuilder<CourseSchedule> builder)
    {
        builder.ToTable("course_schedules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CourseId).HasColumnName("course_id");

        // smallint in the column, DayOfWeek in the model: the same mapping
        // CourseSchedule's own remark describes — 0 Sunday .. 6 Saturday,
        // never a string, never translated on the way in or out.
        builder.Property(x => x.DayOfWeek).HasColumnName("day_of_week").HasConversion<short>();

        builder.Property(x => x.StartsAt).HasColumnName("starts_at");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at");

        builder.HasIndex(x => x.CourseId);
    }
}
