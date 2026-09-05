using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Standings;

/// <summary>
/// The table of a category.
/// </summary>
/// <remarks>
/// Computed on every read rather than kept in a column, and the schema agrees
/// — there is no standings table. A stored table is a second copy of the
/// truth that has to be rebuilt whenever a result is corrected, a match is
/// awarded, a team withdraws or a ruleset is renamed, and the day one of
/// those forgets to rebuild it the competition is publishing a lie. A
/// category holds a handful of teams and a season of matches; adding them up
/// is cheaper than keeping them honest.
/// </remarks>
public static class ReadStandings
{
    /// <param name="Position">
    /// Where the team stands, counting from one within its group. Answered
    /// here because it is what a table is read for, and because a client that
    /// numbered the rows itself would get it wrong the moment two teams are
    /// level and the order is not the ranking.
    /// </param>
    public sealed record Row(
        int Position,
        Guid TeamId,
        string TeamName,
        string? ClubLogoUrl,
        int Played,
        int Won,
        int Drawn,
        int Lost,
        int ScoreFor,
        int ScoreAgainst,
        int ScoreDifference,
        int Points);

    public sealed record Group(string? Label, IReadOnlyList<Row> Rows);

    /// <param name="AllowsDraw">
    /// Whether this ruleset prices a drawn match at all — false for every
    /// sport played in sets, and false for a cumulative one whose organizers
    /// chose not to award a draw. Carried so a table knows whether its own
    /// "empatados" column has anything to say, without a client having to
    /// re-derive the rule from the score mode.
    /// </param>
    /// <param name="Tiebreakers">
    /// The criteria applied, in the order they were applied. Returned so a
    /// table can explain itself: "why is Sur above Norte" is the most asked
    /// question about this object, and the answer is in the ruleset rather
    /// than in the numbers on screen.
    /// </param>
    /// <param name="QualifiersPerGroup">
    /// How many rows of each group to highlight as advancing, per the
    /// organizers' declared rule. Null when there is none to show.
    /// </param>
    public sealed record Response(
        Guid CategoryId,
        string CategoryName,
        string SportCode,
        Guid RulesetId,
        string RulesetName,
        bool AllowsDraw,
        IReadOnlyList<string> Tiebreakers,
        IReadOnlyList<Group> Groups,
        short? QualifiersPerGroup);

    public static IEndpointRouteBuilder MapReadStandings(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/categories/{categoryId:guid}/standings", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadStandings))
            .WithSummary("Builds the standings table of a category.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        ObjectStore store,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await StandingsQuery.ForCategoryAsync(database, categoryId, cancellationToken)
            is not { } table)
        {
            return Results.NotFound();
        }

        return Results.Ok(
            await PresentAsync(table, store, organization.RequireOrganizationId(), cancellationToken));
    }

    /// <summary>
    /// Numbers the rows, signs each crest, and hands the table over.
    /// </summary>
    /// <remarks>
    /// Shared with the public reading, so the two describe the same table the
    /// same way — including the positions, which is the part a caller cannot
    /// recompute correctly when two teams are level.
    ///
    /// The organization is an explicit argument rather than read from
    /// <paramref name="table"/> for the same reason <see cref="ObjectStore"/>
    /// takes it as one: the public reading resolves it from the address, not
    /// from a request context that does not exist there.
    /// </remarks>
    internal static async Task<Response> PresentAsync(
        StandingsResult table,
        ObjectStore store,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var groups = new List<Group>(table.Groups.Count);

        foreach (var group in table.Groups)
        {
            var rows = new List<Row>(group.Rows.Count);

            for (var index = 0; index < group.Rows.Count; index++)
            {
                var row = group.Rows[index];

                var logoUrl = table.LogoKeys.TryGetValue(row.TeamId, out var key)
                    && !string.IsNullOrEmpty(key)
                        ? await store.ReadLinkAsync(organizationId, key, cancellationToken)
                        : null;

                rows.Add(new Row(
                    index + 1,
                    row.TeamId,
                    row.TeamName,
                    logoUrl,
                    row.Played,
                    row.Won,
                    row.Drawn,
                    row.Lost,
                    row.ScoreFor,
                    row.ScoreAgainst,
                    row.ScoreDifference,
                    row.Points));
            }

            groups.Add(new Group(group.Label, rows));
        }

        return new Response(
            table.CategoryId,
            table.CategoryName,
            table.SportCode,
            table.RulesetId,
            table.RulesetName,
            table.AllowsDraw,
            table.Tiebreakers,
            groups,
            table.QualifiersPerGroup);
    }
}
