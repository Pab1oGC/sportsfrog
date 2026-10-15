using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Attendance;

/// <summary>Each student's attendance rate across a course, to date.</summary>
public static class ReadAttendanceSummary
{
    /// <param name="Rate">
    /// Attended over recorded, or null when nothing has been recorded for
    /// this student yet — see <c>AttendanceTally.Rate</c> for why that is
    /// never read as zero.
    /// </param>
    public sealed record Summary(
        Guid EnrollmentId,
        string FirstName,
        string LastName,
        string DocumentId,
        int Recorded,
        int Present,
        int Late,
        int Absent,
        int Excused,
        decimal? Rate);

    public static IEndpointRouteBuilder MapReadAttendanceSummary(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/courses/{courseId:guid}/attendance/summary", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.Course, "courseId"))
            .WithName(nameof(ReadAttendanceSummary))
            .WithSummary("Reads each student's attendance rate for a course.");

        return routes;
    }

    /// <summary>
    /// Everyone ever enrolled, withdrawn or not — a report on a course's
    /// own history wants a student's full record, the same reasoning
    /// <see cref="Enrollments.ReadEnrollments"/> already follows for its
    /// own listing.
    /// </summary>
    internal static async Task<IResult> ListAsync(
        Guid courseId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await database.Courses.AnyAsync(course => course.Id == courseId, cancellationToken))
        {
            return Results.NotFound();
        }

        var sessions = await database.CourseSessions
            .Where(session => session.CourseId == courseId)
            .Select(session => new { session.Id, session.Status })
            .ToListAsync(cancellationToken);

        var marks = await database.AttendanceRecords
            .Where(record => record.Session!.CourseId == courseId)
            .Select(record => new { record.EnrollmentId, record.SessionId, record.Status })
            .ToListAsync(cancellationToken);

        var marksByEnrollment = marks
            .GroupBy(mark => mark.EnrollmentId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(mark => mark.SessionId, mark => mark.Status));

        var enrollments = await database.CourseEnrollments
            .Where(entry => entry.CourseId == courseId)
            .Select(entry => new
            {
                entry.Id,
                entry.Athlete!.FirstName,
                entry.Athlete.LastName,
                entry.Athlete.DocumentId,
            })
            .OrderBy(entry => entry.LastName)
            .ThenBy(entry => entry.FirstName)
            .ToListAsync(cancellationToken);

        var summaries = enrollments
            .Select(entry =>
            {
                marksByEnrollment.TryGetValue(entry.Id, out var ownMarks);

                var entries = sessions
                    .Select(session => new AttendanceEntry(
                        Held: session.Status == CourseSessionState.Held,
                        Mark: ownMarks is not null && ownMarks.TryGetValue(session.Id, out var mark) ? mark : null))
                    .ToList();

                var tally = AttendanceRate.Resolve(entries);

                return new Summary(
                    entry.Id, entry.FirstName, entry.LastName, entry.DocumentId,
                    tally.Recorded, tally.Present, tally.Late, tally.Absent, tally.Excused, tally.Rate);
            })
            .ToList();

        return Results.Ok(summaries);
    }
}
