using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>
/// Strikes an enrollment that should never have been made.
/// </summary>
/// <remarks>
/// The administrative removal — see <see cref="Rosters.StrikeRegistration"/>,
/// its roster counterpart. It is for the wrong person entered on the wrong
/// course, not for a student who left, which is a withdrawal and keeps its
/// record.
///
/// Unconditional for now, unlike StrikeRegistration: there is nothing yet
/// that can be recorded against an enrollment — attendance and pension
/// charges are later migrations — so there is nothing to guard. The day one
/// of those exists, this gains the same question
/// <c>RosterUsage.HasRecordedEventsAsync</c> answers for a roster entry.
/// </remarks>
public static class StrikeEnrollment
{
    public static IEndpointRouteBuilder MapStrikeEnrollment(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/enrollments/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseEnrollment, "id"))
            .WithName(nameof(StrikeEnrollment))
            .WithSummary("Strikes an enrollment that was entered by mistake.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var entry = await database.CourseEnrollments.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
