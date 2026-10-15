using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>A course open to receive a new enrollment, or the reason it is not.</summary>
internal sealed record OpenedCourse(IResult? Refusal, Course? Course);

/// <summary>
/// Everything that has to be true about a course before anyone is enrolled
/// in it.
/// </summary>
/// <remarks>
/// The same question <see cref="Rosters.RosterTeamGate"/> asks of a team,
/// narrower: a course has no competition to have finished, only its own
/// flag.
/// </remarks>
internal static class CourseGate
{
    /// <summary>Opens the course, or answers why not. Null means no such course.</summary>
    public static async Task<OpenedCourse?> OpenAsync(
        Guid courseId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var course = await database.Courses.SingleOrDefaultAsync(
            candidate => candidate.Id == courseId, cancellationToken);

        if (course is null)
        {
            return null;
        }

        if (!course.IsActive)
        {
            return new OpenedCourse(
                Results.Problem(
                    detail: $"\"{course.Name}\" está desactivado, así que no está tomando " +
                            "inscripciones nuevas.",
                    statusCode: StatusCodes.Status409Conflict),
                null);
        }

        return new OpenedCourse(null, course);
    }
}
