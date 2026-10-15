namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One mark: how one student did on one session they were enrolled for.
/// </summary>
/// <remarks>
/// Against the enrollment, not the athlete — the same reasoning
/// <see cref="CourseEnrollment"/> itself documents for hanging attendance
/// and pension charges off an enrollment rather than a person: the same
/// athlete taking the same course across two separate enrollment windows
/// has two histories, not one shared one.
///
/// <see cref="Status"/> is <see cref="SportFrog.Domain.School.AttendanceState"/>,
/// the domain project's own type — not a second enum invented here. That
/// type already exists for <c>AttendanceRate</c> to reduce a student's
/// marks to one rate, and the shape a mark is checked against must not
/// drift from the shape it is reduced by.
/// </remarks>
public sealed class AttendanceRecord
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid SessionId { get; set; }

    public Guid EnrollmentId { get; set; }

    public AttendanceState Status { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CourseSession? Session { get; set; }
    public CourseEnrollment? Enrollment { get; set; }
}
