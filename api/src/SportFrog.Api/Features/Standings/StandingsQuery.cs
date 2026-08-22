using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Standings;

/// <summary>A built table, and what it was built from.</summary>
internal sealed record StandingsResult(
    Guid CategoryId,
    string CategoryName,
    string SportCode,
    Guid RulesetId,
    string RulesetName,
    IReadOnlyList<string> Tiebreakers,
    IReadOnlyList<StandingsGroup> Groups);

/// <summary>
/// Reads what a table is made of and builds it.
/// </summary>
/// <remarks>
/// Shared between the endpoint an organization reads and the one the public
/// page reads, and the sharing is the point rather than a saving. Two
/// implementations of a standings table is two tables, and the day they
/// disagree the argument is not about which is right — it is about which one
/// the league published.
///
/// Takes the context rather than resolving one, because the two callers reach
/// the database by different routes: one through the request's own context,
/// the other through the read-only connection the public reader opens after
/// establishing the organization itself.
/// </remarks>
internal static class StandingsQuery
{
    public static async Task<StandingsResult?> ForCategoryAsync(
        SportFrogDbContext database,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Competition!.SportCode,

                // The category's own ruleset where it has one, the
                // competition's otherwise: a division is ranked by the rules
                // it plays under.
                RulesetId = candidate.RulesetId ?? candidate.Competition.RulesetId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        var sport = await database.Sports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == category.SportCode, cancellationToken);

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == category.RulesetId, cancellationToken);

        if (sport is null || ruleset is null)
        {
            return null;
        }

        // Every team entered, including one that withdrew: it keeps what it
        // played and keeps its place in the table, which is exactly why
        // withdrawing is a flag and not a deletion.
        var contenders = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId)
            .Select(team => new Contender(team.Id, team.Name, team.GroupLabel))
            .ToListAsync(cancellationToken);

        // A match counts once it has a result. Finished is the ordinary way;
        // a walkover is the other, and it counts for the same reason it is a
        // state of its own — nobody played it, but it stands.
        var played = await database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId)
            .Where(match => match.Status == MatchState.Finished
                || match.Status == MatchState.Walkover)
            .Where(match => match.HomeTotal != null && match.AwayTotal != null)
            .Select(match => new PlayedMatch(
                match.HomeTeamId, match.AwayTeamId, match.HomeTotal!.Value, match.AwayTotal!.Value))
            .ToListAsync(cancellationToken);

        return new StandingsResult(
            category.Id,
            category.Name,
            category.SportCode,
            ruleset.Id,
            ruleset.Name,
            ruleset.Config.Tiebreakers,
            StandingsCalculator.Build(contenders, played, sport.ScoreMode, ruleset.Config));
    }
}
