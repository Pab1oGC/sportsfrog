using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff;

/// <summary>
/// Strikes a staff registration that should never have been made.
/// </summary>
/// <remarks>
/// The administrative removal, and the rarer of the two. It is for the wrong
/// person entered on the wrong bench — not for a coach who left, which is a
/// withdrawal and keeps its record.
///
/// Refused once a credential has been issued to that person for this
/// competition: at that point the registration is not a mistake, somebody is
/// carrying a card that says they belong here, and a scan of it still has to
/// resolve. Withdrawing is the honest answer there, and revoking the card is
/// a separate decision — see <c>Features.Documents.RevokeDocument</c>.
///
/// Striking frees the person to be registered again, for this team or
/// another, because the unique index ignores struck rows. That is the point of
/// it.
/// </remarks>
public static class StrikeStaff
{
    public static IEndpointRouteBuilder MapStrikeStaff(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/staff/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.TeamStaff, "id"))
            .WithName(nameof(StrikeStaff))
            .WithSummary("Strikes a staff registration that was entered by mistake.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        StaffUsage usage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // The competition comes through the team's category: a credential is
        // issued per person per competition, which is the pair the usage check
        // asks about.
        var found = await (
            from staff in database.TeamStaff
            join team in database.Teams on staff.TeamId equals team.Id
            join category in database.Categories on team.CategoryId equals category.Id
            where staff.Id == id
            select new { Staff = staff, category.CompetitionId }
        ).SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return Results.NotFound();
        }

        if (await usage.HasIssuedDocumentsAsync(
                found.Staff.AthleteId, found.CompetitionId, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya se emitió una credencial para esta persona en esta competencia, así " +
                        "que el registro no se puede anular. Retirala en su lugar: deja el " +
                        "cuerpo técnico y queda el registro de que estuvo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        found.Staff.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
