using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Sessions;

/// <summary>
/// Materializes a course's weekly schedule into dated sessions, over a
/// range.
/// </summary>
/// <remarks>
/// A thin wrapper around <see cref="SessionPlanner"/>: this is the part that
/// decides which dates still need a row (idempotent — calling it twice over
/// an overlapping range only inserts what is missing) and clamps the
/// request to the course's own life, which the planner itself has no
/// notion of at all.
/// </remarks>
public static class GenerateSessions
{
    /// <summary>
    /// A safety net against a mistyped year, not a real limit — a school
    /// generating a whole year's calendar at once is a legitimate call.
    /// </summary>
    private const int MaximumRangeDays = 400;

    public sealed record Request(DateOnly From, DateOnly To);

    /// <param name="Generated">Sessions this call actually created.</param>
    /// <param name="AlreadyScheduled">
    /// Sessions the range already had, from an earlier call or a
    /// hand-edited one — the other half of idempotency: a caller running
    /// this twice over the same range sees why the count did not move.
    /// </param>
    /// <param name="EffectiveFrom">
    /// The range this call actually used, after clamping to the course's
    /// own <see cref="Course.StartsOn"/>/<see cref="Course.EndsOn"/> — never
    /// wider than what was asked, so a caller is told when their own range
    /// reached past the course's life rather than left wondering why fewer
    /// sessions appeared than expected.
    /// </param>
    public sealed record Response(int Generated, int AlreadyScheduled, DateOnly EffectiveFrom, DateOnly EffectiveTo);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.To)
                .GreaterThanOrEqualTo(request => request.From)
                .WithMessage("La fecha de fin no puede ser anterior a la de inicio.");

            RuleFor(request => request)
                .Must(request => request.To.DayNumber - request.From.DayNumber <= MaximumRangeDays)
                .WithMessage($"El rango no puede superar los {MaximumRangeDays} días. Generá el " +
                             "calendario en tramos más cortos.");
        }
    }

    public static IEndpointRouteBuilder MapGenerateSessions(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/courses/{courseId:guid}/sessions", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.Course, "courseId"))
            .WithName(nameof(GenerateSessions))
            .WithSummary("Materializes a course's weekly schedule into dated sessions.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid courseId,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var course = await database.Courses
            .Include(candidate => candidate.Schedules)
            .SingleOrDefaultAsync(candidate => candidate.Id == courseId, cancellationToken);

        if (course is null)
        {
            return Results.NotFound();
        }

        if (!course.IsActive)
        {
            return Results.Problem(
                detail: $"\"{course.Name}\" está desactivado, así que no se le genera un " +
                        "calendario nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Never wider than the course's own life: a session before it
        // started or after it already ended answers a question nobody
        // asked.
        var effectiveFrom = request.From > course.StartsOn ? request.From : course.StartsOn;
        var effectiveTo = course.EndsOn is { } endsOn && endsOn < request.To ? endsOn : request.To;

        if (effectiveFrom > effectiveTo)
        {
            // The clamp swallowed the whole range — asked entirely before
            // the course started, or entirely after it ended. Reported as
            // zero and zero rather than a validation error: the request
            // itself was well formed, it just landed outside a life this
            // course does not reach.
            return Results.Ok(new Response(0, 0, effectiveFrom, effectiveTo));
        }

        var slots = course.Schedules
            .Select(schedule => new WeeklySlot(schedule.DayOfWeek, schedule.StartsAt, schedule.EndsAt))
            .ToList();

        var planned = SessionPlanner.Plan(slots, effectiveFrom, effectiveTo);

        var existingKeys = await database.CourseSessions
            .Where(session => session.CourseId == courseId)
            .Where(session => session.SessionDate >= effectiveFrom && session.SessionDate <= effectiveTo)
            .Select(session => new { session.SessionDate, session.StartsAt })
            .ToListAsync(cancellationToken);

        var existing = existingKeys.Select(key => (key.SessionDate, key.StartsAt)).ToHashSet();

        var toCreate = planned.Where(session => !existing.Contains((session.Date, session.StartsAt))).ToList();

        // Added to the set directly, not through a Course.Sessions
        // navigation — Course carries none, and even if it did, a freshly
        // assigned Id on a store-generated key is read by EF as "this
        // already exists" when it only reaches the tracker through a
        // parent's collection (see UpdateCourse's own remark on this exact
        // mistake).
        foreach (var session in toCreate)
        {
            database.CourseSessions.Add(new CourseSession
            {
                Id = Guid.NewGuid(),
                OrgId = course.OrgId,
                CourseId = course.Id,
                SessionDate = session.Date,
                StartsAt = session.StartsAt,
                EndsAt = session.EndsAt,
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two calls generating the same range at once. The check above
            // answers the ordinary case; only the partial index sees this
            // one.
            return Results.Problem(
                detail: "Alguien generó el mismo calendario en el mismo instante. Volvé a leerlo " +
                        "e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(new Response(toCreate.Count, planned.Count - toCreate.Count, effectiveFrom, effectiveTo));
    }
}
