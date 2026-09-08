using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Reads a filled-in delegation squad sheet and says what importing it
/// would do.
/// </summary>
/// <remarks>
/// A sibling of <see cref="PreviewRosterImport"/>, scoped to a competition
/// and a club instead of a team, for the individual-sport delegations that
/// have no single team to scope an upload to. Writes nothing, for exactly
/// the reason that one does not: the useful moment to learn that row 14
/// names a category that does not exist is before any of the file is in the
/// database.
/// </remarks>
public static class PreviewDelegationRosterImport
{
    private const long MaximumUpload = 5 * 1024 * 1024;

    public sealed record Row(
        int Number,
        string? Document,
        string Name,
        string? Category,
        ImportOutcome Outcome,
        IReadOnlyList<string> Problems);

    public sealed record Response(
        Guid CompetitionId,
        string CompetitionName,
        Guid ClubId,
        string ClubName,
        int Total,
        int ToRegister,
        int ToCreate,
        int AlreadyRegistered,
        int Rejected,
        IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapPreviewDelegationRosterImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
                "/competitions/{competitionId:guid}/clubs/{clubId:guid}/individuals/import/preview",
                HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(PreviewDelegationRosterImport))
            .WithSummary("Reads a filled-in delegation squad sheet and reports what importing it would do.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid clubId,
        IFormFile file,
        SportFrogDbContext database,
        DelegationRosterImportReview review,
        CancellationToken cancellationToken)
    {
        if (await DelegationRosterImportGate.OpenAsync(
                competitionId, clubId, file, MaximumUpload, database, cancellationToken)
            is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var reviewed = await review.ReviewAsync(
            competitionId, opened.Club!, opened.Categories!, opened.Sheet!.Rows, cancellationToken);

        return Results.Ok(new Response(
            competitionId,
            opened.Competition!.Name,
            opened.Club!.Id,
            opened.Club.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.CategoryName, row.Outcome, row.Problems))]));
    }
}
