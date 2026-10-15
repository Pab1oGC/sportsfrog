using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Attendance;

/// <summary>Reads one session's attendance sheet: its roster, and what is already marked.</summary>
public static class ReadSessionAttendance
{
    /// <param name="Status">
    /// Null when the session ran but nobody has marked this student yet —
    /// a gap in the sheet, not a fifth state. A caller filling it in reads
    /// this as "still to do"; see <c>SportFrog.Domain.School.AttendanceRate</c>
    /// for why that gap is never read as an absence later on.
    /// </param>
    public sealed record Summary(
        Guid EnrollmentId,
        string FirstName,
        string LastName,
        string DocumentId,
        AttendanceState? Status,
        string? Note);

    public static IEndpointRouteBuilder MapReadSessionAttendance(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/sessions/{id:guid}/attendance", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseSession, "id"))
            .WithName(nameof(ReadSessionAttendance))
            .WithSummary("Reads one session's attendance sheet.");

        return routes;
    }

    internal static async Task<IResult> ListAsync(
        Guid id,
        SportFrogDbContext database,
        AttendancePolicy policy,
        CancellationToken cancellationToken)
    {
        var session = await database.CourseSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (session is null)
        {
            return Results.NotFound();
        }

        var validEnrollmentIds = await policy.ValidEnrollmentIdsAsync(
            session.CourseId, session.SessionDate, cancellationToken);

        var marks = await database.AttendanceRecords
            .Where(record => record.SessionId == id)
            .ToDictionaryAsync(record => record.EnrollmentId, cancellationToken);

        var roster = await database.CourseEnrollments
            .Where(entry => validEnrollmentIds.Contains(entry.Id))
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

        var sheet = roster
            .Select(entry =>
            {
                marks.TryGetValue(entry.Id, out var mark);
                return new Summary(entry.Id, entry.FirstName, entry.LastName, entry.DocumentId, mark?.Status, mark?.Note);
            })
            .ToList();

        return Results.Ok(sheet);
    }
}
