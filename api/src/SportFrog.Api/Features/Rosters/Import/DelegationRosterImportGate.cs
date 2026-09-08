using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>An upload that got as far as being worth reviewing, or the reason it did not.</summary>
internal sealed record OpenedDelegationImport(
    IResult? Refusal,
    Competition? Competition,
    Club? Club,
    IReadOnlyList<Category>? Categories,
    DelegationSheetContents? Sheet);

/// <summary>
/// Everything that has to be true before a delegation's squad sheet is
/// worth reading.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterImportGate"/>, scoped to a competition and a
/// club instead of a team. An individual sport has no team to scope an
/// upload to before the file is read — that is exactly what makes this file
/// different from a squad's: each row of it is going to create its own.
/// </remarks>
internal static class DelegationRosterImportGate
{
    /// <summary>Opens the upload, or answers why not. Null means no such competition or club.</summary>
    public static async Task<OpenedDelegationImport?> OpenAsync(
        Guid competitionId,
        Guid clubId,
        IFormFile file,
        long maximumUpload,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Refuse("El archivo está vacío.");
        }

        if (file.Length > maximumUpload)
        {
            return Refuse($"El archivo pesa más de {maximumUpload / (1024 * 1024)} MB.");
        }

        var competition = await database.Competitions
            .AsNoTracking()
            .Include(candidate => candidate.Sport)
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        if (competition?.Sport is not { } sport)
        {
            return null;
        }

        if (!sport.IsIndividual)
        {
            return Conflict(
                $"{sport.Name} no es un deporte individual, así que sus nóminas se cargan por "
                    + "equipo, no por delegación.");
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            return Conflict(
                "Esta competencia ya está en curso, así que no se pueden inscribir más "
                    + "deportistas.");
        }

        var club = await database.Clubs
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == clubId, cancellationToken);

        if (club is null)
        {
            return null;
        }

        if (!club.IsActive)
        {
            return Conflict($"{club.Name} no está activo, así que no se puede inscribir bajo él.");
        }

        var categories = await database.Categories
            .AsNoTracking()
            .Where(category => category.CompetitionId == competitionId)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0)
        {
            return Conflict("Esta competencia todavía no tiene categorías, así que no hay dónde anotar a nadie.");
        }

        using var contents = file.OpenReadStream();
        var sheet = DelegationRosterSheetReader.Read(contents);

        if (sheet.Problem is { } problem)
        {
            return Refuse(problem);
        }

        // The workbook says which competition and club it was made for, and
        // it says another pair. Refused outright rather than reported row by
        // row, for the same reason RosterImportGate refuses a mismatched
        // team: every row would otherwise pass, and the failure this stamp
        // exists to prevent is registering a whole delegation under the
        // wrong one.
        if (sheet.StampedCompetition is { } stampedCompetition && stampedCompetition != competitionId)
        {
            return Refuse(sheet.StampedSubject is { } subject
                ? $"Esta planilla se generó para {subject}, no para esta competencia. Hay que "
                    + "subirla contra esa competencia, o descargar la plantilla de esta."
                : "Esta planilla se generó para otra competencia. Hay que descargar la plantilla "
                    + "de esta.");
        }

        if (sheet.StampedClub is { } stampedClub && stampedClub != clubId)
        {
            return Refuse(sheet.StampedSubject is { } subject
                ? $"Esta planilla se generó para {subject}, no para {club.Name}. Hay que subirla "
                    + $"contra ese club, o descargar la plantilla de {club.Name}."
                : $"Esta planilla se generó para otro club. Hay que descargar la plantilla de "
                    + $"{club.Name}.");
        }

        if (sheet.Rows.Count == 0)
        {
            return Refuse("La hoja tiene los encabezados pero ninguna fila llena.");
        }

        return new OpenedDelegationImport(null, competition, club, categories, sheet);
    }

    /// <summary>Something wrong with the file itself, rather than with a row in it.</summary>
    private static OpenedDelegationImport Refuse(string message) =>
        new(
            Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] }),
            null, null, null, null);

    private static OpenedDelegationImport Conflict(string message) =>
        new(
            Results.Problem(detail: message, statusCode: StatusCodes.Status409Conflict),
            null, null, null, null);
}
