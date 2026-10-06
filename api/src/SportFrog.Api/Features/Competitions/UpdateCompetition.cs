using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Documents;
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
/// Three of the fields stop being editable, two of them only once the
/// competition leaves draft and one from the moment it is created at all.
///
/// The ruleset scores and ranks every match; swapping it once matches exist
/// restates results nobody touched. The capture level decides whether events
/// are attributed to players at all; raising it halfway through leaves a top
/// scorer table built from the second half of the season and presented as the
/// whole of it.
///
/// The slug is the address of the public page, and it is locked from the
/// start rather than once settled: nothing about a fresh draft makes a link
/// to it safe to move. The moment a competition is created, whoever set up
/// the address may have already handed it out — printed it, put it in a
/// group chat — and a competition still in draft is not exempt from that.
/// Renaming the competition costs nothing; renaming its address breaks
/// whatever was shared before whoever changed it thinks to reshare it.
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

    internal static async Task<IResult> HandleAsync(
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

        if (await CredentialDesignChoice.RefuseUnknownAsync(
                contract.CredentialDesignId, database, cancellationToken) is { } unknownDesign)
        {
            return unknownDesign;
        }

        if (Slug.Normalize(contract.Slug) != competition.Slug)
        {
            return Results.Problem(
                detail: "La dirección pública de una competencia queda fija desde que se crea: " +
                        "cambiarla rompería cualquier enlace que ya se haya compartido. Si hace " +
                        "falta otra, creá la competencia de nuevo con la dirección que corresponda.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await CompetitionUniqueness.IsNameTakenAsync(
                database, contract.Name, except: id, cancellationToken))
        {
            return CompetitionUniqueness.NameTaken();
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
        competition.Season = contract.Season.Trim();
        competition.Format = contract.Format;
        competition.CaptureLevel = captureLevel;
        competition.StartsOn = contract.StartsOn;
        competition.EndsOn = contract.EndsOn;
        competition.Settings = settings ?? new CompetitionSettings();
        competition.CredentialDesignId = contract.CredentialDesignId;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (CompetitionUniqueness.ConflictFor(exception) is not null)
        {
            return CompetitionUniqueness.ConflictFor(exception)!;
        }

        // Only once the row is safely saved: a picture forgotten before that
        // and then a rollback would leave the still-referenced key gone.
        await CompetitionPortalPictures.ForgetOrphanedAsync(
            previousPublic, competition.Settings.Public, pictures, cancellationToken);

        return Results.NoContent();
    }
}
