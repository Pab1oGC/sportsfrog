using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Why <see cref="ChampionResolver.ResolveAsync"/> could, or could not, name a team.</summary>
internal enum ChampionResolution
{
    /// <summary>No category has this id — the caller should answer "not found", not an empty list.</summary>
    CategoryNotFound,

    /// <summary>The category exists, but nothing in it answers this position yet, or ever will for this format.</summary>
    Undecided,

    /// <summary><see cref="ChampionResult.TeamId"/> is who finished there.</summary>
    Resolved,
}

/// <summary>One answer to "who finished Nth in this category", and why.</summary>
internal readonly record struct ChampionResult(
    ChampionResolution Resolution, string? CategoryName, Guid? TeamId, string? Reason)
{
    public static ChampionResult NotFound() => new(ChampionResolution.CategoryNotFound, null, null, null);

    public static ChampionResult Undecided(string categoryName, string reason) =>
        new(ChampionResolution.Undecided, categoryName, null, reason);

    public static ChampionResult Resolved(string categoryName, Guid teamId) =>
        new(ChampionResolution.Resolved, categoryName, teamId, null);
}

/// <summary>
/// Who finished in a given place in a category — first, second, or further
/// down a table or a classification stage.
/// </summary>
/// <remarks>
/// A generalization of <c>Public.ReadPublicCompetition.ResolveChampionAsync</c>,
/// which only ever answers position one, for the portal's own cover. Reading
/// it twice — once there for a headline, once here for an export — had to
/// stay one rule: both read the bracket the same way, by its last round
/// rather than by a hardcoded final phase, and both hand the actual winner
/// off to <see cref="MatchWinner"/> rather than compare scores themselves.
///
/// What is new here: a second position (the loser of that same final), a
/// judged category answered from <see cref="ClassificationRanking"/> — which
/// the portal's reading never had to, since a judged category runs no bracket
/// and no table for it to fall back to either — and an honest
/// <see cref="ChampionResolution.Undecided"/> instead of a silent null, so a
/// caller exporting a list can say why there is nothing to export instead of
/// handing back an empty file with no explanation.
///
/// Deliberately does not attempt a knockout's third place: that is decided by
/// a repechage ladder's own bronze match in formats that run one, one bronze
/// per half of the draw, and sometimes none at all when a half had nobody
/// left to award it to automatically — a different and more involved question
/// than this resolver answers for every other position, left for the day a
/// caller actually needs it.
/// </remarks>
internal static class ChampionResolver
{
    private sealed record BracketMatch(
        short? RoundNumber,
        Guid? HomeTeamId,
        Guid? AwayTeamId,
        int? HomeTotal,
        int? AwayTotal,
        Guid? WalkoverTeamId,
        short? PenaltyHomeScore,
        short? PenaltyAwayScore);

    public static async Task<ChampionResult> ResolveAsync(
        SportFrogDbContext database, Guid categoryId, int position, CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(candidate => new { candidate.Name, candidate.Competition!.SportCode })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return ChampionResult.NotFound();
        }

        if (position < 1)
        {
            return ChampionResult.Undecided(category.Name, "Ese puesto no existe.");
        }

        var scoreMode = await database.Sports
            .AsNoTracking()
            .Where(sport => sport.Code == category.SportCode)
            .Select(sport => (ScoreMode?)sport.ScoreMode)
            .SingleOrDefaultAsync(cancellationToken);

        if (scoreMode == ScoreMode.Judged)
        {
            return await ResolveFromClassificationAsync(database, categoryId, category.Name, position, cancellationToken);
        }

        var bracket = await database.Matches
            .AsNoTracking()
            // A repechage ladder carries a phase too, but it decides its own
            // bronze independently of this final — the same exclusion
            // ResolveChampionAsync applies, for the same reason: left in, its
            // matches could outrank the actual final and get read as though
            // they were it.
            .Where(match => match.CategoryId == categoryId && match.Phase != null && !match.IsRepechage)
            .OrderByDescending(match => match.RoundNumber)
            .Select(match => new BracketMatch(
                match.RoundNumber, match.HomeTeamId, match.AwayTeamId, match.HomeTotal, match.AwayTotal,
                match.WalkoverTeamId, match.PenaltyHomeScore, match.PenaltyAwayScore))
            .ToListAsync(cancellationToken);

        if (bracket.Count > 0)
        {
            return ResolveFromBracket(bracket, category.Name, position);
        }

        return await ResolveFromStandingsAsync(database, categoryId, category.Name, position, cancellationToken);
    }

    /// <summary>
    /// The team at a position, read from a decided knockout final. Never
    /// falls back to a table: once a category has a bracket, that bracket —
    /// not whatever group stage it may have grown out of — is what decided
    /// it, the same distinction <c>StandingsQuery</c> itself draws by format.
    /// </summary>
    private static ChampionResult ResolveFromBracket(
        IReadOnlyList<BracketMatch> bracket, string categoryName, int position)
    {
        var lastRound = bracket[0].RoundNumber;
        var finalists = bracket.Where(match => match.RoundNumber == lastRound).ToList();

        if (finalists.Count != 1)
        {
            // The last round drawn still has more than one tie in it — not
            // actually a final yet, whatever the competition's own status
            // says.
            return ChampionResult.Undecided(categoryName, "La final de esta categoría todavía no está definida.");
        }

        var final = finalists[0];

        if (final.HomeTeamId is not { } homeId || final.AwayTeamId is not { } awayId)
        {
            // A knockout drawn in full books its final before it knows who
            // reaches it.
            return ChampionResult.Undecided(categoryName, "La final de esta categoría todavía no está definida.");
        }

        Guid? winner = MatchWinner.Resolve(
            homeId, awayId, final.WalkoverTeamId, final.HomeTotal, final.AwayTotal,
            final.PenaltyHomeScore, final.PenaltyAwayScore);

        if (winner is null)
        {
            return ChampionResult.Undecided(categoryName, "La final de esta categoría todavía no se jugó.");
        }

        return position switch
        {
            1 => ChampionResult.Resolved(categoryName, winner.Value),
            2 => ChampionResult.Resolved(categoryName, winner == homeId ? awayId : homeId),
            _ => ChampionResult.Undecided(
                categoryName, "Esta categoría no define un tercer puesto a partir de la llave."),
        };
    }

    /// <summary>
    /// The team at a position, read from the one table a league — or a group
    /// stage nobody ever promoted — has. Real groups that were never
    /// promoted to a knockout have one table per group, and none of them
    /// alone speaks for the category, the same rule
    /// <c>ResolveChampionAsync</c> already follows for position one.
    /// </summary>
    private static async Task<ChampionResult> ResolveFromStandingsAsync(
        SportFrogDbContext database, Guid categoryId, string categoryName, int position,
        CancellationToken cancellationToken)
    {
        var standings = await StandingsQuery.ForCategoryAsync(database, categoryId, cancellationToken);

        if (standings?.Groups is not [{ Rows: { } rows }])
        {
            return ChampionResult.Undecided(
                categoryName, "Esta categoría tiene más de un grupo, y ninguno por sí solo define este puesto.");
        }

        if (position > rows.Count)
        {
            return ChampionResult.Undecided(categoryName, "Esta categoría no tiene tantos equipos.");
        }

        var row = rows[position - 1];

        if (row.Played == 0)
        {
            return ChampionResult.Undecided(categoryName, "Todavía no hay resultados cargados en esta categoría.");
        }

        return ChampionResult.Resolved(categoryName, row.TeamId);
    }

    /// <summary>The team at a position, read from a judged category's classification stage.</summary>
    private static async Task<ChampionResult> ResolveFromClassificationAsync(
        SportFrogDbContext database, Guid categoryId, string categoryName, int position,
        CancellationToken cancellationToken)
    {
        var entries = await PerformancesQuery.ForCategoryAsync(database, categoryId, cancellationToken);
        var ranked = ClassificationRanking.Rank(entries);

        foreach (var (rankedPosition, entry) in ranked)
        {
            if (rankedPosition == position)
            {
                return ChampionResult.Resolved(categoryName, entry.TeamId);
            }
        }

        return ChampionResult.Undecided(categoryName, "Todavía no hay resultados cargados en esta categoría.");
    }
}
