using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>Something an enrollment says that the course does not admit.</summary>
internal sealed record EnrollmentViolation(string Property, string Message);

/// <summary>
/// Whether a person may be enrolled in a course.
/// </summary>
/// <remarks>
/// Shared between enrolling and reinstating, which ask the same question —
/// see <see cref="Rosters.RosterPolicy"/>, the roster's own version of this
/// split.
/// A correction has to leave the enrollment being reinstated out of its own
/// collision check, or a student would collide with themselves.
/// </remarks>
internal sealed class EnrollmentPolicy(SportFrogDbContext database)
{
    /// <param name="excluding">
    /// The enrollment being reinstated, left out of the count and the
    /// collision check. Absent when enrolling someone new.
    /// </param>
    /// <param name="pending">
    /// Enrollments already accepted earlier in the same bulk request but
    /// not yet written — see <see cref="EnrollStudentsBulk"/>. Zero for a
    /// single enrollment, which is the only kind that existed before that;
    /// without this, the second name in a batch would read the course's
    /// capacity as whatever it was before the batch started, the same gap
    /// <see cref="Rosters.RosterPolicy"/>'s own <c>pending</c> closes for a
    /// team.
    /// </param>
    public async Task<IReadOnlyList<EnrollmentViolation>> InspectAsync(
        Course course,
        Athlete athlete,
        Guid? excluding,
        CancellationToken cancellationToken,
        int pending = 0)
    {
        var violations = new List<EnrollmentViolation>();

        if (!athlete.IsActive)
        {
            violations.Add(new EnrollmentViolation(
                "AthleteId",
                $"{athlete.FirstName} {athlete.LastName} no está activo en la organización, así " +
                "que no se lo puede inscribir."));
        }

        // One enrollment per person per course, the same shape
        // RosterPolicy.InspectPlaceAsync checks across a whole category —
        // simpler here because a course has no equivalent of a sibling team
        // to look across. Withdrawn counts: the partial unique index
        // (uq_course_enrollments_course_athlete) does not exclude it either,
        // on purpose, and the message below is what tells a caller why the
        // plain insert would fail instead of leaving them to find out from
        // a raw conflict.
        var elsewhere = await database.CourseEnrollments
            .Where(entry => entry.Id != excluding)
            .Where(entry => entry.CourseId == course.Id && entry.AthleteId == athlete.Id)
            .Select(entry => new { Withdrawn = entry.WithdrawnAt != null })
            .FirstOrDefaultAsync(cancellationToken);

        if (elsewhere is not null)
        {
            violations.Add(new EnrollmentViolation(
                "AthleteId",
                elsewhere.Withdrawn
                    ? $"La inscripción de {athlete.FirstName} {athlete.LastName} en este curso " +
                      "figura como retirada. Para que vuelva a tomarlo hay que reintegrar esa " +
                      "misma inscripción, no crear una nueva."
                    : $"{athlete.FirstName} {athlete.LastName} ya está inscripto en este curso."));
        }

        if (course.Capacity is { } capacity)
        {
            var enrolled = await database.CourseEnrollments
                .Where(entry => entry.Id != excluding)
                .Where(entry => entry.CourseId == course.Id && entry.WithdrawnAt == null)
                .CountAsync(cancellationToken);

            if (enrolled + pending >= capacity)
            {
                violations.Add(new EnrollmentViolation(
                    "AthleteId",
                    $"\"{course.Name}\" ya tiene el cupo de {capacity} que admite. Hay que " +
                    "retirar a alguien antes de inscribir a otra persona."));
            }
        }

        return violations;
    }
}
