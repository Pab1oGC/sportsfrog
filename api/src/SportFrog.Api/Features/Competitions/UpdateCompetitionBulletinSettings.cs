using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Corrects a competition's bulletin — the organizer's own prose, printed
/// with it — on its own, leaving the rest of the competition untouched.
/// </summary>
/// <remarks>
/// <see cref="UpdateCompetition"/> still answers <c>PUT /competitions/{id}</c>
/// with the whole contract, and still replaces <c>settings</c> whole when
/// called that way. This is the narrower correction: a screen whose only job
/// is the bulletin's text should not have to resend the ruleset, the public
/// page's theme or the scheduling settings just to change a sentence, the
/// way <c>portal-studio-save.js</c> on the client side has to today to avoid
/// losing them. The two settings sections a caller might correct this way —
/// this one and <see cref="UpdateCompetitionPublicSettings"/> — each have
/// their own endpoint for the same reason: a client correcting one never
/// risks the other.
/// </remarks>
public static class UpdateCompetitionBulletinSettings
{
    public static IEndpointRouteBuilder MapUpdateCompetitionBulletinSettings(this IEndpointRouteBuilder routes)
    {
        routes.MapPatch("/competitions/{id:guid}/settings/bulletin", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "id"))
            .WithName(nameof(UpdateCompetitionBulletinSettings))
            .WithSummary("Corrects a competition's bulletin.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        BulletinSettings? request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        // Null clears the section, the same shape UpdateCompetition's own
        // mapToSend already uses for "nothing filled in" — not an object
        // with four empty strings.
        competition.Settings = competition.Settings with { Bulletin = request };

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else corrected this same competition between when
            // this request read it and when it saved — UpdateCompetition,
            // a lifecycle transition, or this same endpoint called twice.
            return Results.Problem(
                detail: "Alguien más cambió esta competencia mientras la tenías abierta. Volvé " +
                        "a leerla antes de guardar de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
