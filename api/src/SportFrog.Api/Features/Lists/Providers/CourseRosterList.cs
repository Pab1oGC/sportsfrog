using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Everyone ever enrolled in a course, current students first and those who left after.</summary>
/// <remarks>
/// The course's own equivalent of <see cref="RosterList"/>: a plain read of
/// one course's enrollments rather than a shared helper, since a course has
/// no sibling provider that reads the same shape a second way — unlike
/// <see cref="TeamRosterRows"/>, which exists precisely because
/// <see cref="RosterList"/> and <see cref="RosterByPositionList"/> both
/// read a team's squad.
///
/// Everyone ever enrolled, withdrawn or not, same reasoning
/// <c>Courses.Enrollments.ReadEnrollments</c> and
/// <c>Courses.Attendance.ReadAttendanceSummary</c> already follow for their
/// own listings: a roster handed to someone outside this screen is a
/// record of the course's history, not only of today's class list.
/// </remarks>
internal sealed class CourseRosterList : IListProvider
{
    private static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Documento", ListValueKind.Text),
        new ListColumn("Apellido y nombre", ListValueKind.Text),
        new ListColumn("Inscripto", ListValueKind.Date),
        new ListColumn("Pensión (Bs)", ListValueKind.Number),
        new ListColumn("Estado", ListValueKind.Text),
    ];

    public string Slug => "plantel-curso";

    public string Label => "Plantel de un curso";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("courseId", ListParameterKind.Course, Required: true)];

    // A course's own enrollment list is the same question regardless of
    // which sport it teaches, and has nothing to do with a competition's
    // score mode in the first place — same reasoning AthletesList gives
    // for its own unconditional true.
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

        var enrollments = await database.CourseEnrollments
            .AsNoTracking()
            .Where(entry => entry.CourseId == courseId)
            .OrderBy(entry => entry.WithdrawnAt != null)
            .ThenBy(entry => entry.Athlete!.LastName)
            .ThenBy(entry => entry.Athlete!.FirstName)
            .Select(entry => new
            {
                entry.Athlete!.DocumentId,
                entry.Athlete.FirstName,
                entry.Athlete.LastName,
                entry.EnrolledAt,
                entry.WithdrawnAt,
                Fee = entry.FeeOverrideBob ?? entry.Course!.MonthlyFeeBob,
            })
            .ToListAsync(cancellationToken);

        var rows = enrollments
            .Select(entry => (IReadOnlyList<object?>)
            [
                entry.DocumentId,
                $"{entry.LastName}, {entry.FirstName}",
                DateOnly.FromDateTime(entry.EnrolledAt.UtcDateTime),
                entry.Fee,
                entry.WithdrawnAt is null ? "Activo" : "Retirado",
            ])
            .ToList();

        return new ListTable($"Plantel — {courseName}", null, Columns, [new ListSection(null, rows)]);
    }
}
