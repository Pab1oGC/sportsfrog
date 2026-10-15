using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Every student's attendance rate across one course, to date.</summary>
/// <remarks>
/// The same question <c>Courses.Attendance.ReadAttendanceSummary</c> answers
/// for its own screen, read again here rather than shared with it — that
/// handler exists to drive a table on a page, this one to hand the same
/// facts to a renderer that knows nothing about either. Both resolve a
/// student's rate the one way <see cref="AttendanceRate.Resolve"/> defines
/// it, so the two can never disagree about what a rate means, even though
/// neither calls the other.
/// </remarks>
internal sealed class AttendanceList : IListProvider
{
    private static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Documento", ListValueKind.Text),
        new ListColumn("Apellido y nombre", ListValueKind.Text),
        new ListColumn("Clases registradas", ListValueKind.Number),
        new ListColumn("Presente", ListValueKind.Number),
        new ListColumn("Tarde", ListValueKind.Number),
        new ListColumn("Con aviso", ListValueKind.Number),
        new ListColumn("Ausente", ListValueKind.Number),
        new ListColumn("Asistencia (%)", ListValueKind.Number),
    ];

    public string Slug => "asistencia";

    public string Label => "Asistencia de un curso";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("courseId", ListParameterKind.Course, Required: true)];

    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("courseId") is not { } courseId)
        {
            return null;
        }

        var courseName = await database.Courses
            .AsNoTracking()
            .Where(course => course.Id == courseId)
            .Select(course => (string?)course.Name)
            .SingleOrDefaultAsync(cancellationToken);

        if (courseName is null)
        {
            return null;
        }

        var sessions = await database.CourseSessions
            .AsNoTracking()
            .Where(session => session.CourseId == courseId)
            .Select(session => new { session.Id, session.Status })
            .ToListAsync(cancellationToken);

        var marks = await database.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.Session!.CourseId == courseId)
            .Select(record => new { record.EnrollmentId, record.SessionId, record.Status })
            .ToListAsync(cancellationToken);

        var marksByEnrollment = marks
            .GroupBy(mark => mark.EnrollmentId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(mark => mark.SessionId, mark => mark.Status));

        var enrollments = await database.CourseEnrollments
            .AsNoTracking()
            .Where(entry => entry.CourseId == courseId)
            .OrderBy(entry => entry.Athlete!.LastName)
            .ThenBy(entry => entry.Athlete!.FirstName)
            .Select(entry => new
            {
                entry.Id,
                entry.Athlete!.FirstName,
                entry.Athlete.LastName,
                entry.Athlete.DocumentId,
            })
            .ToListAsync(cancellationToken);

        var rows = enrollments
            .Select(entry =>
            {
                marksByEnrollment.TryGetValue(entry.Id, out var ownMarks);

                var entries = sessions
                    .Select(session => new AttendanceEntry(
                        Held: session.Status == CourseSessionState.Held,
                        Mark: ownMarks is not null && ownMarks.TryGetValue(session.Id, out var mark) ? mark : null))
                    .ToList();

                var tally = AttendanceRate.Resolve(entries);

                return (IReadOnlyList<object?>)
                [
                    entry.DocumentId,
                    $"{entry.LastName}, {entry.FirstName}",
                    tally.Recorded,
                    tally.Present,
                    tally.Late,
                    tally.Excused,
                    tally.Absent,
                    tally.Rate is { } rate ? Math.Round(rate * 100, 1) : null,
                ];
            })
            .ToList();

        return new ListTable($"Asistencia — {courseName}", null, Columns, [new ListSection(null, rows)]);
    }
}
