using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Reads a filled-in squad sheet and says what registering it would do.
/// </summary>
/// <remarks>
/// Writes nothing. That is the entire point: an operator uploads a file
/// somebody else filled in, and the useful moment to learn that row 14 has an
/// unreadable date and row 22 names a player who already turns out for a
/// rival is before any of it is in the database. An import that loaded what
/// it could and listed the rest afterwards would leave a squad half entered
/// and the correction to be made against rows that are now real.
///
/// Safe to call as many times as anybody likes, which means the interface can
/// call it the moment a file is chosen rather than behind a confirmation.
/// </remarks>
public static class PreviewRosterImport
{
    /// <summary>
    /// Past any squad sheet and short of anything worth streaming. The file
    /// is parsed in memory to answer inside the request.
    /// </summary>
    private const long MaximumUpload = 5 * 1024 * 1024;

    public sealed record Row(
        int Number,
        string? Document,
        string Name,
        ImportOutcome Outcome,
        IReadOnlyList<string> Problems);

    public sealed record Response(
        Guid TeamId,
        string TeamName,
        string CategoryName,
        int Total,
        int ToRegister,
        int ToCreate,
        int AlreadyRegistered,
        int Rejected,
        IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapPreviewRosterImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/roster/import/preview", HandleAsync)
            .RequireRole(MembershipRole.Operator)

            // The caller is a bearer token, not a cookie, so there is no
            // session for another site to ride on and nothing for an
            // antiforgery token to protect. Left on, the framework refuses
            // every upload on this route.
            .DisableAntiforgery()
            .WithName(nameof(PreviewRosterImport))
            .WithSummary("Reads a filled-in squad sheet and reports what importing it would do.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        IFormFile file,
        SportFrogDbContext database,
        RosterImportReview review,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Refuse("El archivo está vacío.");
        }

        if (file.Length > MaximumUpload)
        {
            return Refuse($"El archivo pesa más de {MaximumUpload / (1024 * 1024)} MB.");
        }

        var team = await database.Teams
            .AsNoTracking()
            .Include(candidate => candidate.Category)
                .ThenInclude(category => category!.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);

        if (team?.Category is not { Competition: { } competition } category)
        {
            return Results.NotFound();
        }

        // The same two refusals a single registration makes, made here as
        // well so the preview does not promise something the import would
        // then decline forty times over.
        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            return Results.Problem(
                detail: "Esta competencia terminó, así que sus planteles están cerrados.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!team.IsActive)
        {
            return Results.Problem(
                detail: $"{team.Name} se retiró de esta categoría, así que no está recibiendo "
                        + "inscripciones.",
                statusCode: StatusCodes.Status409Conflict);
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

        var reviewed = await review.ReviewAsync(team, category, sheet.Rows, cancellationToken);

        return Results.Ok(new Response(
            team.Id,
            team.Name,
            category.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.Outcome, row.Problems))]));
    }

    /// <summary>
    /// Something wrong with the file itself, rather than with a row in it.
    /// </summary>
    private static IResult Refuse(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["file"] = [message],
        });
}
