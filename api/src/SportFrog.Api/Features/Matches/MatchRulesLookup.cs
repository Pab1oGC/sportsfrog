using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Rulebook;
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
                candidate.Competition.Format,
                candidate.Competition.Sport!.ScoreMode,
                candidate.CategoryId,
                candidate.Phase,
                CategoryRulesetId = candidate.Category!.RulesetId,
                CompetitionRulesetId = candidate.Competition.RulesetId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            return null;
        }

        // This fixture's own stage — see EffectiveRuleset and
        // CompetitionStages.KindOfMatch — overriding the category's where it
        // has one, the competition's otherwise.
        var stageKind = CompetitionStages.KindOfMatch(context.Format, context.ScoreMode, context.Phase);

        var stageRulesetId = await database.CategoryStages
            .AsNoTracking()
            .Where(stage => stage.CategoryId == context.CategoryId && stage.Kind == stageKind)
            .Select(stage => stage.RulesetId)
            .SingleOrDefaultAsync(cancellationToken);

        var rulesetId = EffectiveRuleset.Of(
            stageRulesetId, context.CategoryRulesetId, context.CompetitionRulesetId);

        var sport = await database.Sports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == context.SportCode, cancellationToken);

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == rulesetId, cancellationToken);

        return sport is null || ruleset is null ? null : new MatchRules(sport, ruleset.Config);
    }
}
