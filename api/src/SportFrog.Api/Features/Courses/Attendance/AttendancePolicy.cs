using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Courses.Attendance;

/// <summary>
/// Whether attendance may be recorded at all, and against whom.
/// </summary>
/// <remarks>
/// Two separate questions, asked by <see cref="RecordAttendance"/> in this
/// order: whether the session itself is open to it
/// (<see cref="InspectSession"/>), and which enrollments a mark on it may
/// actually name (<see cref="ValidEnrollmentIdsAsync"/>). A cancelled or
/// not-yet-run session fails the first before the second is ever asked.
/// </remarks>
internal sealed class AttendancePolicy(SportFrogDbContext database, TimeProvider clock)
{
    /// <summary>
    /// Why this session cannot have attendance recorded against it right
    /// now, or null when it can.
    /// </summary>
    public string? InspectSession(CourseSession session)
    {
        if (session.Status == CourseSessionState.Cancelled)
        {
            return "Esta clase está cancelada, así que no se le puede tomar asistencia.";
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        if (session.SessionDate > today)
        {
            return "Esta clase todavía no se dictó, así que no se le puede tomar asistencia.";
        }

        return null;
    }

    /// <summary>
    /// The enrollments of this course that were actually students as of
    /// this session's date: already enrolled by then, and — if they later
    /// left — not withdrawn before it.
    /// </summary>
    /// <remarks>
    /// Compared as instants rather than as calendar dates (the session's
    /// own <see cref="CourseSession.SessionDate"/> read as the UTC day it
    /// names) because <see cref="CourseEnrollment.EnrolledAt"/> and
    /// <see cref="CourseEnrollment.WithdrawnAt"/> are themselves instants —
    /// comparing them against a bare date one way for one side and another
    /// way for the other is how an off-by-one enters a boundary check.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> ValidEnrollmentIdsAsync(
        Guid courseId,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        var sessionStart = new DateTimeOffset(sessionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var sessionEnd = sessionStart.AddDays(1);

        var ids = await database.CourseEnrollments
            .Where(entry => entry.CourseId == courseId)
            .Where(entry => entry.EnrolledAt < sessionEnd)
            .Where(entry => entry.WithdrawnAt == null || entry.WithdrawnAt >= sessionStart)
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
