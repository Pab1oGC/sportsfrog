using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>
/// Takes a student out of a course, or puts them back.
/// </summary>
/// <remarks>
/// The same shape as <see cref="Rosters.WithdrawPlayer"/>: the sporting
/// (here, administrative-but-ordinary) removal, reversible because a
/// withdrawal is a claim about the world and claims are sometimes entered
/// wrongly. Coming back is checked exactly as a first enrollment is — the
/// course may have filled while they were away.
/// </remarks>
public static class WithdrawEnrollment
{
    public sealed record Request(bool Withdrawn);

    public static IEndpointRouteBuilder MapWithdrawEnrollment(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/enrollments/{id:guid}/withdrawal", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseEnrollment, "id"))
            .WithName(nameof(WithdrawEnrollment))
            .WithSummary("Withdraws a student from a course, or reinstates them.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        EnrollmentPolicy policy,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var entry = await database.CourseEnrollments
            .Include(candidate => candidate.Course)
            .Include(candidate => candidate.Athlete)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (entry?.Course is not { } course || entry.Athlete is not { } athlete)
        {
            return Results.NotFound();
        }

        if (request.Withdrawn == entry.WithdrawnAt.HasValue)
        {
            // Already where the caller is asking for. A second click on
            // "withdraw" is not a mistake to report.
            return Results.NoContent();
        }

        if (!request.Withdrawn)
        {
            var violations = await policy.InspectAsync(course, athlete, excluding: id, cancellationToken);

            if (violations.Count > 0)
            {
                return Results.ValidationProblem(violations
                    .GroupBy(violation => violation.Property)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(violation => violation.Message).ToArray()));
            }
        }

        entry.WithdrawnAt = request.Withdrawn ? clock.GetUtcNow() : null;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Reinstating at the same instant someone else enrolled this
            // same person through a fresh row — unreachable in practice
            // (EnrollmentPolicy would have refused that one first), kept for
            // the same reason WithdrawPlayer keeps its own: only the index
            // sees a true race.
            return Results.Problem(
                detail: "Esa inscripción choca con un cambio hecho en el mismo instante. Volvé " +
                        "a leer la lista e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
