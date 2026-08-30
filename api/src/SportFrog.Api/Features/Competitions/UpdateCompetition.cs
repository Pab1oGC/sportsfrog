using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Corrects a competition of the active organization.
/// </summary>
/// <remarks>
/// Two of the fields stop being editable once the competition leaves draft,
/// and the reason is the same in both cases: they decide how what is played
/// gets read afterwards.
///
/// The ruleset scores and ranks every match; swapping it once matches exist
/// restates results nobody touched. The capture level decides whether events
/// are attributed to players at all; raising it halfway through leaves a top
/// scorer table built from the second half of the season and presented as the
/// whole of it.
///
/// Everything else — the name, the season, the dates, what the public page
/// shows — is description, and description gets corrected.
/// </remarks>
public static class UpdateCompetition
{
    public static IEndpointRouteBuilder MapUpdateCompetition(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(UpdateCompetition))
            .WithSummary("Corrects a competition.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        CompetitionContract contract,
        SportFrogDbContext database,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        var settled = competition.Status != CompetitionState.Draft;

        if (settled && contract.RulesetId != competition.RulesetId)
        {
            return Results.Problem(
                detail: "Esta competencia ya salió de borrador, así que su reglamento quedó " +
                        "fijo. Los resultados ya registrados se leen contra él, y reemplazarlo " +
                        "ahora los reescribiría sin que nadie edite un partido.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var captureLevel = WireEnum.Parse<CaptureLevel>(contract.CaptureLevel);

        if (settled && captureLevel != competition.CaptureLevel)
        {
            return Results.Problem(
                detail: "Esta competencia ya salió de borrador, así que cuánto detalle registra " +
                        "quedó fijo. Cambiarlo ahora dejaría una temporada medida de una forma " +
                        "al principio y de otra al final.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (contract.RulesetId != competition.RulesetId)
        {
            var replacement = await database.Rulesets
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == contract.RulesetId, cancellationToken);

            if (replacement is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["RulesetId"] = ["No ruleset of this organization has that identifier."],
                });
            }

            competition.RulesetId = replacement.Id;

            // Kept in step with the ruleset, because it was never an
            // independent answer: a competition under a volleyball ruleset is
            // a volleyball competition.
            competition.SportCode = replacement.SportCode;
        }

        var slug = Slug.Normalize(contract.Slug);

        if (await database.Competitions.AnyAsync(
                other => other.Id != id && other.Slug == slug, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya hay una competencia usando esa dirección.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var previousPublic = competition.Settings.Public;
        var settings = contract.Settings;

        if (settings?.Public is { } requestedPublic)
        {
            if (await CompetitionPortalPictures.ResolveAsync(requestedPublic, pictures, cancellationToken)
                is not { } resolvedPublic)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Settings.Public"] = ["Alguna imagen del portal público no se pudo leer."],
                });
            }

            settings = settings with { Public = resolvedPublic };
        }

        competition.Name = contract.Name.Trim();
        competition.Slug = slug;
        competition.Season = contract.Season.Trim();
        competition.Format = contract.Format;
        competition.CaptureLevel = captureLevel;
        competition.StartsOn = contract.StartsOn;
        competition.EndsOn = contract.EndsOn;
        competition.Settings = settings ?? new CompetitionSettings();

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Ya hay una competencia usando esa dirección.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Only once the row is safely saved: a picture forgotten before that
        // and then a rollback would leave the still-referenced key gone.
        await CompetitionPortalPictures.ForgetOrphanedAsync(
            previousPublic, competition.Settings.Public, pictures, cancellationToken);

        return Results.NoContent();
    }
}
