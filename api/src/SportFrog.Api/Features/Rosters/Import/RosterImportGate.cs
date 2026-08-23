using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>An upload that got as far as being worth reviewing, or the reason it did not.</summary>
internal sealed record OpenedImport(
    IResult? Refusal,
    Team? Team,
    Category? Category,
    SheetContents? Sheet);

/// <summary>
/// Everything that has to be true before a squad sheet is worth reading.
/// </summary>
/// <remarks>
/// Shared between the preview and the import for the same reason the
/// eligibility rules are: the preview exists to tell an operator what the
/// import will do, and a preview that opens a file the import would refuse is
/// a preview that lied. Two copies of these checks would drift, and the
/// looser one would be the one that mattered.
/// </remarks>
internal static class RosterImportGate
{
    /// <summary>
    /// Opens the upload, or answers why not. Null means no such team.
    /// </summary>
    public static async Task<OpenedImport?> OpenAsync(
        Guid teamId,
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

        var team = await database.Teams
            .Include(candidate => candidate.Category)
                .ThenInclude(category => category!.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);

        if (team?.Category is not { Competition: { } competition } category)
        {
            return null;
        }

        // The same two refusals a single registration makes. Made here as
        // well so a preview never promises something the registration would
        // then decline forty times over.
        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            return Conflict("Esta competencia terminó, así que sus planteles están cerrados.");
        }

        if (!team.IsActive)
        {
            return Conflict($"{team.Name} se retiró de esta categoría, así que no está recibiendo "
                + "inscripciones.");
        }

        using var contents = file.OpenReadStream();
        var sheet = RosterSheetReader.Read(contents);

        if (sheet.Problem is { } problem)
        {
            return Refuse(problem);
        }

        // The workbook says which team it was made for, and it says another
        // one. Refused outright rather than reported row by row, because
        // every row would pass: a squad sheet for the under-fifteens is a
        // perfectly valid squad sheet, and registering it for the wrong club
        // is the failure this stamp exists to prevent.
        if (sheet.StampedTeam is { } stamped && stamped != team.Id)
        {
            return Refuse(sheet.StampedSubject is { } subject
                ? $"Esta planilla se generó para {subject}, no para {team.Name}. Hay que subirla "
                    + "contra ese equipo, o descargar la plantilla de este."
                : $"Esta planilla se generó para otro equipo. Hay que descargar la plantilla de "
                    + $"{team.Name}.");
        }

        if (sheet.Rows.Count == 0)
        {
            return Refuse("La hoja tiene los encabezados pero ninguna fila llena.");
        }

        return new OpenedImport(null, team, category, sheet);
    }

    /// <summary>Something wrong with the file itself, rather than with a row in it.</summary>
    private static OpenedImport Refuse(string message) =>
        new(
            Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] }),
            null, null, null);

    private static OpenedImport Conflict(string message) =>
        new(
            Results.Problem(detail: message, statusCode: StatusCodes.Status409Conflict),
            null, null, null);
}
