namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A person registered to take a course.
/// </summary>
/// <remarks>
/// This is what makes a student's attendance and pension charges possible:
/// they are recorded against an enrollment, not against a person, so the
/// same athlete taking the same course across two separate enrollment
/// windows produces two enrollments and two separate histories — the same
/// reasoning <see cref="RosterEntry"/> already documents for a roster.
/// </remarks>
public sealed class CourseEnrollment
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CourseId { get; set; }

    public Guid AthleteId { get; set; }

    public DateTimeOffset EnrolledAt { get; set; }

    /// <summary>When they stopped taking this course, if they did.</summary>
    public DateTimeOffset? WithdrawnAt { get; set; }

    /// <summary>
    /// This enrollment's own price, in bolivianos, replacing
    /// <see cref="Course.MonthlyFeeBob"/> — a scholarship at zero, or a
    /// discount at any other figure. Null means this enrollment pays
    /// whatever the course charges. Always set together with
    /// <see cref="FeeOverrideReason"/>; never one without the other.
    /// </summary>
    public decimal? FeeOverrideBob { get; set; }

    public string? FeeOverrideReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Course? Course { get; set; }
    public Athlete? Athlete { get; set; }
}
