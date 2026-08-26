using Npgsql;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// The front page of the public portal: what there is to look at.
/// </summary>
/// <remarks>
/// Every other public reading is reached by somebody who already knows the
/// address. This is the one a visitor arrives at knowing nothing, so it is
/// also the only one that crosses organizations — and that is what makes it
/// different in kind rather than in degree.
///
/// It cannot go through the isolation policies, because there is no single
/// organization to establish a context for; a directory of one league is not
/// a directory. So the crossing is done in exactly one place, by a function
/// owned by the schema owner that returns only what a listing shows, only for
/// competitions somebody chose to publish, and never anything else. Nothing
/// here reads a table directly.
///
/// What it returns is deliberately enough to build the link and no more. The
/// two slugs are the address of the detail page; everything else is what goes
/// on a card.
/// </remarks>
public static class ReadPublicCompetitions
{
    /// <param name="OrganizationSlug">
    /// First half of the address of the detail page:
    /// <c>/public/{organizationSlug}/{competitionSlug}</c>.
    /// </param>
    /// <param name="Categories">
    /// How many divisions it runs, so a card can say "3 categorías" without a
    /// second request per competition.
    /// </param>
    public sealed record Listed(
        string OrganizationSlug,
        string OrganizationName,
        string CompetitionSlug,
        string CompetitionName,
        string SportCode,
        string SportName,
        string Season,
        CompetitionState Status,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        int Categories,
        int Teams);

    /// <param name="Total">
    /// How many there are in all, for pagination. Not the length of
    /// <c>Competitions</c>, which is one page of them.
    /// </param>
    public sealed record Response(int Total, int Skip, int Take, IReadOnlyList<Listed> Competitions);

    /// <summary>Enough for a page of cards, and a ceiling somebody cannot raise.</summary>
    private const int DefaultTake = 24;

    public static IEndpointRouteBuilder MapReadPublicCompetitions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/public/competitions", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicCompetitions))
            .WithSummary("Lists every published competition, for the public portal.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        [FromKeyedServices(PublicCompetitionReader.PublicDataSourceKey)] NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        string? sport = null,
        string? season = null,
        string? search = null,
        int skip = 0,
        int take = DefaultTake)
    {
        // A visitor followed a link or typed something. Nothing here is worth
        // a refusal: an unknown sport narrows the list to nothing, which is a
        // readable answer, and a silly page size is clamped rather than
        // argued with.
        var wantedSport = QueryFilter.OrAbsent(sport);
        var wantedSeason = QueryFilter.OrAbsent(season);
        var wantedSearch = QueryFilter.OrAbsent(search);

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT organization_slug, organization_name,
                   competition_slug, competition_name,
                   sport_code, sport_name, season, status,
                   starts_on, ends_on, categories, teams, total
            FROM list_public_competitions($1, $2, $3, $4, $5)
            """,
            connection);

        command.Parameters.Add(Text(wantedSport));
        command.Parameters.Add(Text(wantedSeason));
        command.Parameters.Add(Text(wantedSearch));
        command.Parameters.Add(new NpgsqlParameter { Value = take });
        command.Parameters.Add(new NpgsqlParameter { Value = skip });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var listed = new List<Listed>(take);
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            listed.Add(new Listed(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetFieldValue<CompetitionState>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9),
                (int)reader.GetInt64(10),
                (int)reader.GetInt64(11)));

            total = (int)reader.GetInt64(12);
        }

        return Results.Ok(new Response(total, skip, take, listed));
    }

    /// <summary>
    /// A text parameter that may be absent.
    /// </summary>
    /// <remarks>
    /// Typed explicitly because Npgsql cannot infer the type of a null, and
    /// an untyped null reaches the function as unknown rather than as text —
    /// which the planner refuses rather than treats as "no filter".
    /// </remarks>
    private static NpgsqlParameter Text(string? value) =>
        new() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)value ?? DBNull.Value };
}
