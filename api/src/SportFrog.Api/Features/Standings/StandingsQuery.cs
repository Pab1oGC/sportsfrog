using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Standings;

/// <summary>A built table, and what it was built from.</summary>
/// <param name="LogoKeys">
/// A team's club crest, by team, as a storage key rather than a link — the
/// table is built once and presented twice (the organization's own reading
/// and the public one), and only the presentation step knows which
/// organization to sign a link against.
/// </param>
/// <param name="QualifiersPerGroup">
/// How many rows of each group the organizers declared as advancing, for the
/// table to highlight — not a record of what a knockout draw actually did
/// with them. Null when never declared, or when the format is not Groups.
/// </param>
internal sealed record StandingsResult(
    Guid CategoryId,
    string CategoryName,
    string SportCode,
    Guid RulesetId,
    string RulesetName,

    /// <summary>
    /// Whether this ruleset prices a drawn match at all.
    /// </summary>
    /// <remarks>
    /// False for every sport played in sets — the mode has no draw to price,
    /// and <c>RulesetPolicy</c> would refuse one that tried. False too for a
    /// cumulative sport whose organizers simply chose not to award one, which
    /// <see cref="MatchOutcomes.OptionalFor"/> allows precisely because
    /// whether a match can end level is the competition's call, not the
    /// sport's. Either way, a table with nobody drawn and nothing priced for
    /// it is not publishing a column that always reads zero — the question
    /// does not apply here.
    /// </remarks>
    bool AllowsDraw,

    IReadOnlyList<string> Tiebreakers,
    IReadOnlyList<StandingsGroup> Groups,
    IReadOnlyDictionary<Guid, string?> LogoKeys,
    short? QualifiersPerGroup);

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
                candidate.Competition.Format,

                // The category's own ruleset where it has one, the
                // competition's otherwise: a division is ranked by the rules
                // it plays under.
                RulesetId = candidate.RulesetId ?? candidate.Competition.RulesetId,

                candidate.QualifiersPerGroup,
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

        // A knockout-only category has no group stage at all — every match
        // is a single-elimination tie, not a round of a table. Folding those
        // into a standings table would count a bracket win as if it were a
        // league result (three points for winning a quarterfinal, the same
        // three for winning a group match), which answers a question the
        // category never asked. The public page shows the bracket instead;
        // see Sections.Standings in ReadPublicCompetition, which refuses the
        // section outright for this format rather than show a table with
        // nothing honest to say.
        if (category.Format == CompetitionFormat.Knockout)
        {
            return null;
        }

        // Every team entered, including one that withdrew: it keeps what it
        // played and keeps its place in the table, which is exactly why
        // withdrawing is a flag and not a deletion.
        var teams = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId)
            .Select(team => new { team.Id, team.Name, team.GroupLabel, LogoKey = team.Club!.LogoUrl })
            .ToListAsync(cancellationToken);

        var contenders = teams
            .Select(team => new Contender(team.Id, team.Name, team.GroupLabel))
            .ToList();

        var logoKeys = teams.ToDictionary(team => team.Id, team => team.LogoKey);

        // A match counts once it has a result. Finished is the ordinary way;
        // a walkover is the other, and it counts for the same reason it is a
        // state of its own — nobody played it, but it stands.
        //
        // A category that promoted to a knockout has two kinds of match
        // going forward, told apart by Phase: group matches, which is what
        // this table is, and knockout matches, which are not. A single-
        // elimination win is not worth three points added to a table that
        // already closed — the table this builds is the group stage's, and
        // once there is a knockout on the same category, only Groups format
        // has both to tell apart at all.
        var played = await database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId)
            .Where(match => category.Format != CompetitionFormat.Groups || match.Phase == null)
            .Where(match => match.Status == MatchState.Finished
                || match.Status == MatchState.Walkover)
            .Where(match => match.HomeTotal != null && match.AwayTotal != null)
            // Finished or walkover already implies both teams were named —
            // neither is reachable otherwise.
            .Select(match => new PlayedMatch(
                match.HomeTeamId!.Value, match.AwayTeamId!.Value, match.HomeTotal!.Value, match.AwayTotal!.Value))
            .ToListAsync(cancellationToken);

        return new StandingsResult(
            category.Id,
            category.Name,
            category.SportCode,
            ruleset.Id,
            ruleset.Name,
            ruleset.Config.Points.ContainsKey(MatchOutcomes.Draw),
            ruleset.Config.Tiebreakers,
            StandingsCalculator.Build(contenders, played, sport.ScoreMode, ruleset.Config),
            logoKeys,
            category.QualifiersPerGroup);
    }
}
