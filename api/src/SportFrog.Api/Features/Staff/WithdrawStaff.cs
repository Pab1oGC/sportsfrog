using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff;

/// <summary>
/// Takes a staff member off a team, or puts them back.
/// </summary>
/// <remarks>
/// The ordinary removal, and the one that is nearly always right. It says the
/// person stopped working for this team, and keeps the record that they did —
/// which is what a credential printed in their name is already evidence of.
///
/// Reversible, because a withdrawal is a claim about the world and claims are
/// sometimes entered wrongly. Coming back is not a formality though: the head
/// coach's seat may have been filled while they were gone, so re-entering is
/// checked exactly as a first registration is.
/// </remarks>
public static class WithdrawStaff
{
    public sealed record Request(bool Withdrawn);

    public static IEndpointRouteBuilder MapWithdrawStaff(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/staff/{id:guid}/withdrawal", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.TeamStaff, "id"))
            .WithName(nameof(WithdrawStaff))
            .WithSummary("Withdraws a staff member from a team, or reinstates them.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        StaffPolicy policy,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var staff = await database.TeamStaff.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (staff is null)
        {
            return Results.NotFound();
        }

        if (request.Withdrawn == staff.WithdrawnAt.HasValue)
        {
            // Already where the caller is asking for. Answered as success: a
            // second click on "retirar" is not a mistake to report.
            return Results.NoContent();
        }

        if (!request.Withdrawn)
        {
            // Coming back is a registration again. The seat may have been
            // taken while they were away, so the same rules answer both.
            var violations = await policy.InspectAsync(
                staff.TeamId, staff.AthleteId, staff.Role, excluding: id, cancellationToken);

            if (violations.Count > 0)
            {
                return StaffPolicy.Refuse(violations);
            }
        }

        // From the same clock the rest of the application is tested against,
        // and recorded rather than merely flagged: when somebody left is what
        // a credential issued in their name has to be read against.
        staff.WithdrawnAt = request.Withdrawn ? clock.GetUtcNow() : null;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Reinstating into a seat somebody claimed at the same moment. The
            // index counts only staff actually working here, which is exactly
            // the set this rejoins.
            return Results.Problem(
                detail: "Ese cargo se ocupó en el mismo instante. Volvé a leer el cuerpo técnico " +
                        "e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
