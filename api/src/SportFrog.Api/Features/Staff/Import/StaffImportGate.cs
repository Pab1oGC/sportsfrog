using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>An upload that got as far as being worth reviewing, or the reason it did not.</summary>
internal sealed record OpenedStaffImport(IResult? Refusal, Team? Team, StaffSheetContents? Sheet);

/// <summary>
/// Everything that has to be true before a staff sheet is worth reading.
/// </summary>
/// <remarks>
/// A sibling of <c>Features.Rosters.Import.RosterImportGate</c>, but shorter:
/// a bench is registrable wherever <see cref="RegisterStaff"/> already says
/// it is, so the team-and-competition checks are not restated here, they are
/// asked of <see cref="RosterTeamGate"/> — the exact gate that endpoint
/// itself opens. Two copies of "no such team, competition already over, team
/// withdrawn" would drift, and the looser one would be the one that
/// mattered. What this adds on top is only what a file upload needs that a
/// single registration does not: a size limit, a sheet that parses, and a
/// stamp that names the right team.
/// </remarks>
internal static class StaffImportGate
{
    /// <summary>
    /// Opens the upload, or answers why not. Null means no such team.
    /// </summary>
    public static async Task<OpenedStaffImport?> OpenAsync(
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

        if (await RosterTeamGate.OpenAsync(teamId, database, cancellationToken) is not { } opened)
        {
            return null;
        }

        if (opened.Refusal is { } refusal)
        {
            return new OpenedStaffImport(refusal, null, null);
        }

        var team = opened.Team!;

        using var contents = file.OpenReadStream();
        var sheet = StaffSheetReader.Read(contents);

        if (sheet.Problem is { } problem)
        {
            return Refuse(problem);
        }

        // The workbook says which team it was made for, and it says another
        // one — refused outright for the same reason RosterImportGate
        // refuses a mismatched squad sheet: every row would otherwise pass,
        // and registering a whole bench under the wrong team is exactly the
        // failure the stamp exists to catch.
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

        return new OpenedStaffImport(null, team, sheet);
    }

    /// <summary>Something wrong with the file itself, rather than with a row in it.</summary>
    private static OpenedStaffImport Refuse(string message) =>
        new(Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] }), null, null);
}
