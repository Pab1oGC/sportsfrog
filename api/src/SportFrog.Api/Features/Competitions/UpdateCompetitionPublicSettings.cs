using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Corrects what a visitor is shown on a competition's public page, on its
/// own, leaving the rest of the competition untouched.
/// </summary>
/// <remarks>
/// The portal studio is the one caller: today it saves through
/// <c>PUT /competitions/{id}</c>, which replaces <c>settings</c> whole, so
/// <c>portal-studio-save.js</c> exists purely to reread and resend the
/// schedule and bulletin sections intact — a competition's calendar
/// settings or its printed bulletin would otherwise disappear the instant
/// someone opened the portal studio and saved. This endpoint is the
/// narrower correction that makes that reread-and-resend unnecessary: a
/// caller that only means to change the public page's theme or what it
/// shows cannot reach anything else. See
/// <see cref="UpdateCompetitionBulletinSettings"/> for the same shape
/// applied to the other free-standing settings section.
/// </remarks>
public static class UpdateCompetitionPublicSettings
{
    public static IEndpointRouteBuilder MapUpdateCompetitionPublicSettings(this IEndpointRouteBuilder routes)
    {
        routes.MapPatch("/competitions/{id:guid}/settings/public", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "id"))
            .WithName(nameof(UpdateCompetitionPublicSettings))
            .WithSummary("Corrects a competition's public page.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        PublicSettings? request,
        SportFrogDbContext database,
        PortalPicture pictures,
        IPublicQueryCache publicCache,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        var previousPublic = competition.Settings.Public;
        var resolved = request;

        if (request is not null)
        {
            if (await CompetitionPortalPictures.ResolveAsync(request, pictures, cancellationToken)
                is not { } picturesResolved)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Public"] = ["Alguna imagen del portal público no se pudo leer."],
                });
            }

            resolved = picturesResolved;
        }

        // Null clears the section, the same shape every other optional part
        // of a competition's settings already uses for "nothing set".
        competition.Settings = competition.Settings with { Public = resolved };

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else corrected this same competition between when
            // this request read it and when it saved — UpdateCompetition, a
            // lifecycle transition, or this same endpoint called twice.
            return Results.Problem(
                detail: "Alguien más cambió esta competencia mientras la tenías abierta. Volvé " +
                        "a leerla antes de guardar de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // The public page, if anyone published one for this competition,
        // is exactly what this just corrected. Invalidating a key nobody
        // cached is a no-op.
        publicCache.Invalidate(PublicCacheKey.CompetitionPage(id));

        // Only once the row is safely saved: a picture forgotten before that
        // and then a rollback would leave the still-referenced key gone.
        await CompetitionPortalPictures.ForgetOrphanedAsync(
            previousPublic, competition.Settings.Public, pictures, cancellationToken);

        return Results.NoContent();
    }
}
