using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Attendance;

/// <summary>
/// Records a session's whole attendance sheet in one call.
/// </summary>
/// <remarks>
/// The whole sheet, not one student at a time: a teacher fills it in once
/// and sends it, the same shape <see cref="Rosters.RegisterPlayersBulk"/>
/// accepts a squad. Taking attendance at all is also what first moves a
/// session from <see cref="CourseSessionState.Scheduled"/> to
/// <see cref="CourseSessionState.Held"/> — nothing else in this project
/// does, the same way a match is never marked finished by a button of its
/// own, only by a result being recorded.
///
/// Marking again replaces what was there, row by row: a student corrected
/// from <c>Absent</c> to <c>Late</c> five minutes later is an update, not a
/// second mark racing the first.
/// </remarks>
public static class RecordAttendance
{
    public sealed record Mark(Guid EnrollmentId, AttendanceState Status, string? Note);

    public sealed record Request(IReadOnlyList<Mark> Marks);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Marks)
                .NotEmpty().WithMessage("La planilla necesita al menos una marca.");

            RuleFor(request => request.Marks)
                .Must(marks => marks.Select(mark => mark.EnrollmentId).Distinct().Count() == marks.Count)
                .When(request => request.Marks.Count > 0)
                .WithMessage("Hay una inscripción marcada más de una vez en la misma planilla.");

            RuleForEach(request => request.Marks)
                .ChildRules(mark => mark.RuleFor(m => m.Note).MaximumLength(300));
        }
    }

    public static IEndpointRouteBuilder MapRecordAttendance(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/sessions/{id:guid}/attendance", HandleAsync)
            // The narrowest job in the school: taking a sheet should not
            // need the authority to cancel a class or move its calendar —
            // same reasoning RecordResult's own role carries for a match.
            .RequireRole(MembershipRole.Recorder)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseSession, "id"))
            .WithName(nameof(RecordAttendance))
            .WithSummary("Records a session's attendance sheet.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
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

        if (policy.InspectSession(session) is { } refusal)
        {
            return Results.Problem(detail: refusal, statusCode: StatusCodes.Status409Conflict);
        }

        var validEnrollmentIds = await policy.ValidEnrollmentIdsAsync(
            session.CourseId, session.SessionDate, cancellationToken);

        if (request.Marks.Any(mark => !validEnrollmentIds.Contains(mark.EnrollmentId)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Marks"] = ["Una o más inscripciones no corresponden a este curso, o todavía no " +
                             "estaban activas (o ya se habían retirado) en la fecha de esta clase."],
            });
        }

        var existing = await database.AttendanceRecords
            .Where(record => record.SessionId == id)
            .ToDictionaryAsync(record => record.EnrollmentId, cancellationToken);

        foreach (var mark in request.Marks)
        {
            var note = string.IsNullOrWhiteSpace(mark.Note) ? null : mark.Note.Trim();

            if (existing.TryGetValue(mark.EnrollmentId, out var record))
            {
                record.Status = mark.Status;
                record.Note = note;
            }
            else
            {
                // Added to the set directly, not through a navigation
                // collection — see UpdateCourse's own remark on why a
                // freshly assigned key reaching the tracker only through a
                // parent's collection is read back as "this already
                // exists".
                database.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    OrgId = session.OrgId,
                    SessionId = session.Id,
                    EnrollmentId = mark.EnrollmentId,
                    Status = mark.Status,
                    Note = note,
                });
            }
        }

        // The first successful sheet is what makes the class "held" — see
        // this type's own remarks.
        if (session.Status == CourseSessionState.Scheduled)
        {
            session.Status = CourseSessionState.Held;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two people submitting the same sheet at once. The check above
            // (existing, read moments earlier) answers the ordinary case;
            // only the unique constraint sees this one.
            return Results.Problem(
                detail: "Alguien más guardó esta planilla en el mismo instante. Volvé a leerla " +
                        "e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
