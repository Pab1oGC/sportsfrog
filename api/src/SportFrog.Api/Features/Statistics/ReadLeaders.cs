using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Statistics;

/// <summary>
/// Who leads a category in each of the things it records.
/// </summary>
/// <remarks>
/// Derived on every read, like the table and for the same reason: a stored
/// leaderboard is a second copy of the truth that goes stale the moment an
/// event is corrected, and nothing would say so.
///
/// One board per metric rather than one number per player, because that is
/// what a competition publishes — top scorer, most cards — and because the
/// metrics of five sports do not form a single shape anybody could tabulate
/// together.
/// </remarks>
public static class ReadLeaders
{
    public sealed record Leader(
        int Position,
        Guid RosterEntryId,
        Guid AthleteId,
        string FirstName,
        string LastName,
        short? JerseyNumber,
        Guid TeamId,
        string TeamName,
        int Total);

    /// <param name="MetricId">
    /// Null for the points board once it combines more than one scoring
    /// metric — nothing single identifies free throws, field goals and
    /// three-pointers added up together. <see cref="MetricCode"/> is
    /// <c>"points"</c> there, and is what a client should key on instead.
    /// </param>
    /// <param name="AffectsScore">
    /// Whether this metric moves the match score. Carried so a client can
    /// tell the scoring boards from the disciplinary ones without keeping its
    /// own list of codes.
    /// </param>
    public sealed record Board(
        Guid? MetricId,
        string MetricCode,
        string MetricLabel,
        bool AffectsScore,
        IReadOnlyList<Leader> Leaders);

    public sealed record Response(
        Guid CategoryId,
        string CategoryName,
        string SportCode,
        IReadOnlyList<Board> Boards);

    public static IEndpointRouteBuilder MapReadLeaders(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/categories/{categoryId:guid}/leaders", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadLeaders))
            .WithSummary("Ranks the players of a category in each recorded metric.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        int top = 10)
    {
        if (top is < 1 or > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["top"] = ["A board shows between 1 and 100 places."],
            });
        }

        if (await LeadersQuery.ForCategoryAsync(database, categoryId, top, cancellationToken)
            is not { } boards)
        {
            return Results.NotFound();
        }

        return Results.Ok(Present(boards));
    }

    /// <summary>
    /// Hands the boards over, in the one shape both readings use.
    /// </summary>
    internal static Response Present(LeadersResult boards) =>
        new(
            boards.CategoryId,
            boards.CategoryName,
            boards.SportCode,
            [.. boards.Boards.Select(board => new Board(
                board.MetricId,
                board.MetricCode,
                board.MetricLabel,
                board.AffectsScore,
                [.. board.Leaders.Select(entry => new Leader(
                    entry.Position,
                    entry.Player.RosterEntryId,
                    entry.Player.AthleteId,
                    entry.Player.FirstName,
                    entry.Player.LastName,
                    entry.Player.JerseyNumber,
                    entry.Player.TeamId,
                    entry.Player.TeamName,
                    entry.Player.Total))]))]);
}
