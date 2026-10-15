using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Sessions;

/// <summary>
/// Moves one session to another date or time — a holiday, a venue clash,
/// anything that makes the standing schedule wrong for one afternoon
/// without being wrong in general.
/// </summary>
/// <remarks>
/// Refused on a cancelled session: rescheduling something struck from the
/// calendar is not a correction of a date, it is a reinstatement that
/// happens to also want a new one — <see cref="CancelSession"/> first,
/// then this.
/// </remarks>
public static class RescheduleSession
{
    public sealed record Request(DateOnly SessionDate, TimeOnly StartsAt, TimeOnly EndsAt);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.EndsAt)
                .GreaterThan(request => request.StartsAt)
                .WithMessage("La hora de fin debe ser posterior a la de inicio.");
    }

    public static IEndpointRouteBuilder MapRescheduleSession(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/sessions/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseSession, "id"))
            .WithName(nameof(RescheduleSession))
            .WithSummary("Moves a session to another date or time.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var session = await database.CourseSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (session is null)
        {
            return Results.NotFound();
        }

        if (session.Status == CourseSessionState.Cancelled)
        {
            return Results.Problem(
                detail: "Esta clase está cancelada, así que no se puede reprogramar. " +
                        "Reintegrala primero.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await database.CourseSessions.AnyAsync(
                other => other.Id != id
                    && other.CourseId == session.CourseId
                    && other.SessionDate == request.SessionDate
                    && other.StartsAt == request.StartsAt,
                cancellationToken))
        {
            return Results.Problem(
                detail: "Este curso ya tiene una sesión en esa fecha y hora.",
                statusCode: StatusCodes.Status409Conflict);
        }

        session.SessionDate = request.SessionDate;
        session.StartsAt = request.StartsAt;
        session.EndsAt = request.EndsAt;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two reschedules racing for the same date and time. The check
            // above answers the ordinary case; only the partial index sees
            // this one.
            return Results.Problem(
                detail: "Este curso ya tiene una sesión en esa fecha y hora.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
