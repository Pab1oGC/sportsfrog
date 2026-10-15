using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Course"/> onto the <c>courses</c> table.</summary>
internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.SportCode).HasColumnName("sport_code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.VenueSpaceId).HasColumnName("venue_space_id");
        builder.Property(x => x.CoachMembershipId).HasColumnName("coach_membership_id");
        builder.Property(x => x.Capacity).HasColumnName("capacity");
        builder.Property(x => x.MonthlyFeeBob).HasColumnName("monthly_fee").HasColumnType("numeric(12,2)");
        builder.Property(x => x.StartsOn).HasColumnName("starts_on");
        builder.Property(x => x.EndsOn).HasColumnName("ends_on");
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

        // Partial, like clubs.uq_clubs_name: a soft-deleted course keeps its
        // row, and with it its name, forever — excluding it is what frees
        // the name for a new course to reuse.
        builder.HasIndex(x => new { x.OrgId, x.Name })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // Same as ClubConfiguration: every query sees only the live rows
        // unless it goes out of its way not to, so a soft-deleted course
        // cannot resurface by a feature simply forgetting to filter it out.
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VenueSpace)
            .WithMany()
            .HasForeignKey(x => x.VenueSpaceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CoachMembership)
            .WithMany()
            .HasForeignKey(x => x.CoachMembershipId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Schedules)
            .WithOne(schedule => schedule.Course)
            .HasForeignKey(schedule => schedule.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
