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
        if (await RosterImportGate.OpenAsync(
                teamId, file, MaximumUpload, database, cancellationToken) is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var reviewed = await review.ReviewAsync(
            opened.Team!, opened.Category!, opened.Sheet!.Rows, cancellationToken);

        return Results.Ok(new Response(
            opened.Team!.Id,
            opened.Team.Name,
            opened.Category!.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.Outcome, row.Problems))]));
    }
}
