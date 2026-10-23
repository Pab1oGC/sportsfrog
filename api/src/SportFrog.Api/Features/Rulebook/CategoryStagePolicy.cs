using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Whether a category has already played anything under one of its stages.
/// </summary>
/// <remarks>
/// Narrower than <see cref="RulesetUsage"/>, which answers whether a
/// reglamento is in use anywhere and is about deleting one. This is about a
/// single stage of a single category, and it is what guards changing a
/// <see cref="CategoryStage.RulesetId"/>: the group stage of a volleyball
/// league can still be given its own reglamento the week its bracket starts,
/// because nothing has been played under the bracket's yet — the same
/// reasoning <c>UpdateRuleset</c> already applies to a whole reglamento,
/// asked here one stage at a time instead of one competition at a time.
///
/// A fixture's stage is derived the same way <see cref="MatchRulesLookup"/>
/// and <see cref="MatchEvents.EventPolicy"/> already resolve one — see
/// <see cref="CompetitionStages.KindOfMatch"/> — rather than read off a
/// column, because nothing stores it on the <c>Match</c> row itself.
/// </remarks>
internal sealed class CategoryStagePolicy(SportFrogDbContext database)
{
    public async Task<bool> HasPlayedMatchesAsync(
        Guid categoryId, StageKind kind, CancellationToken cancellationToken)
    {
        var context = await database.Categories
            .AsNoTracking()
            .Where(category => category.Id == categoryId)
            .Select(category => new
            {
                category.Competition!.Format,
                category.Competition.Sport!.ScoreMode,
            })
            .SingleAsync(cancellationToken);

        // Phase alone is pushed to SQL; which stage a Phase means is a
        // branching rule (CompetitionStages.KindOfMatch) that EF has no way
        // to translate, so it runs after the played fixtures — ordinarily a
        // short list — come back.
        var playedPhases = await database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId
                && (match.Status == MatchState.Finished || match.Status == MatchState.Walkover))
            .Select(match => match.Phase)
            .ToListAsync(cancellationToken);

        return playedPhases.Any(phase =>
            CompetitionStages.KindOfMatch(context.Format, context.ScoreMode, phase) == kind);
    }
}
