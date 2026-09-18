using Npgsql;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// What just happened, or is happening, across every published competition —
/// the feed the landing hero reads to float real results instead of
/// decoration.
/// </summary>
/// <remarks>
/// Same reason and shape as <see cref="ReadPublicCompetitions"/>: a visitor on
/// the landing page has not opened any one competition yet, so there is no
/// organization to establish a context for, and this has to cross every one
/// of them the way an ordinary public reading never does. See
/// <c>list_public_recent_results</c> for the row-level policy that is what
/// actually lets this see anything.
/// </remarks>
public static class ReadPublicRecentResults
{
    public sealed record Result(
        string OrganizationSlug,
        string CompetitionSlug,
        string CompetitionName,
        string CategoryName,
        string SportCode,
        string SportName,
        string HomeTeamName,
        string AwayTeamName,
        int? HomeTotal,
        int? AwayTotal,
        MatchState Status,
        DateTimeOffset? ScheduledAt);

    public sealed record Response(IReadOnlyList<Result> Results);

    /// <summary>Enough to fill a ticker without turning it into a second directory.</summary>
    private const int DefaultTake = 8;

    public static IEndpointRouteBuilder MapReadPublicRecentResults(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/public/recent-results", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicRecentResults))
            .WithSummary("What just happened, or is happening, across every published competition.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        [FromKeyedServices(PublicCompetitionReader.PublicDataSourceKey)] NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        int take = DefaultTake)
    {
        take = Math.Clamp(take, 1, 24);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT organization_slug, competition_slug, competition_name, category_name,
                   sport_code, sport_name, home_team_name, away_team_name,
                   home_total, away_total, status, scheduled_at
            FROM list_public_recent_results($1)
            """,
            connection);

        command.Parameters.Add(new NpgsqlParameter { Value = take });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<Result>(take);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new Result(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.GetFieldValue<MatchState>(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)));
        }

        return Results.Ok(new Response(results));
    }
}
