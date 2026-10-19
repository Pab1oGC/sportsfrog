using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Records that a sanctioned person's physical credential is being held
/// back, or returned.
/// </summary>
/// <remarks>
/// Not a revocation — see <see cref="Sanction.CredentialWithheldAt"/> and
/// <see cref="SportFrog.Api.Features.Documents.RevokeDocument"/>'s own
/// remarks for the distinction. The card itself stays <c>issued</c> and keeps resolving the
/// same way at the public verification endpoint; this is only the fact,
/// recorded against the sanction, that whoever holds the credential has it
/// taken from them at the gate.
///
/// Independent of whether the sanction is otherwise served: an organizer may
/// return a card before every term is met, or keep holding one past the
/// point the terms are satisfied, and both are decisions this records rather
/// than something <c>SanctionLedger</c> infers.
/// </remarks>
public static class WithholdCredential
{
    public sealed record Request(bool Withheld);

    public static IEndpointRouteBuilder MapWithholdCredential(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{competitionId:guid}/sanctions/{id:guid}/credential-withholding", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(WithholdCredential))
            .WithSummary("Withholds a sanctioned person's credential at the gate, or returns it.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var sanction = await database.Sanctions.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId, cancellationToken);

        if (sanction is null)
        {
            return Results.NotFound();
        }

        if (request.Withheld == sanction.CredentialWithheldAt.HasValue)
        {
            // Already where the caller is asking for. Answered as success:
            // a second click on "retener" is not a mistake to report.
            return Results.NoContent();
        }

        sanction.CredentialWithheldAt = request.Withheld ? clock.GetUtcNow() : null;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
