using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Finds the sport and effective ruleset behind a fixture.
/// </summary>
/// <remarks>
/// Split out of <see cref="ResultPolicy"/>: this is a database read and
/// nothing else, and it does not belong in the same class as the rules that
/// judge what it finds. Whether a score is legal should be testable without
/// a database, and finding a match's rules should not need to know what a
/// legal score looks like.
/// </remarks>
internal sealed class MatchRulesLookup(SportFrogDbContext database)
{
    /// <summary>
    /// The sport and the effective ruleset behind a fixture.
    /// </summary>
    public async Task<MatchRules?> FindRulesAsync(Match match, CancellationToken cancellationToken)
    {
        var context = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.Id == match.Id)
            .Select(candidate => new
            {
                candidate.Competition!.SportCode,

                // The override where the category sets one, and the
                // competition's otherwise. Resolved in the query so the
                // fallback is not a rule every caller has to remember.
                RulesetId = candidate.Category!.RulesetId ?? candidate.Competition.RulesetId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            return null;
        }

        var sport = await database.Sports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == context.SportCode, cancellationToken);

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == context.RulesetId, cancellationToken);

        return sport is null || ruleset is null ? null : new MatchRules(sport, ruleset.Config);
    }
}
