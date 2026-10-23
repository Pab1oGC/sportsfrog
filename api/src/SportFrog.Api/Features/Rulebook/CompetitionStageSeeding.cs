using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// The <see cref="CompetitionStage"/> and <see cref="CategoryStage"/> rows a
/// freshly created competition or category already implies, from its own
/// format and sport — the same rows
/// <c>20261022000000_AddCompetitionAndCategoryStages</c> backfilled once for
/// everything that existed before this did.
/// </summary>
/// <remarks>
/// Written in the same save as the row it is for, the same choice
/// <see cref="SportFrog.Api.Features.Accreditation.StartingCatalog"/> makes
/// for a competition's starting accreditation catalogue: a competition or
/// category created without its stages would leave the reglamento-per-stage
/// override with nowhere to attach until somebody noticed and backfilled it
/// by hand.
/// </remarks>
internal static class CompetitionStageSeeding
{
    public static IReadOnlyList<CompetitionStage> BuildCompetitionStages(
        Competition competition, ScoreMode scoreMode) =>
        CompetitionStages.For(competition.Format, scoreMode)
            .Select(kind => new CompetitionStage
            {
                Id = Guid.NewGuid(),
                OrgId = competition.OrgId,
                CompetitionId = competition.Id,
                Kind = kind,
            })
            .ToList();

    /// <remarks>
    /// <see cref="Category.QualifiersPerGroup"/> and
    /// <see cref="Category.UsesRepechage"/> are mirrored onto the matching
    /// stage, the same split the backfill migration already made for every
    /// category that existed before this — see that migration's own
    /// remarks for why the columns on <see cref="Category"/> stay as the
    /// columns a caller still reads and writes, with this table holding a
    /// faithful copy rather than being the only place either value lives,
    /// for now.
    /// </remarks>
    public static IReadOnlyList<CategoryStage> BuildCategoryStages(
        Category category, string format, ScoreMode scoreMode) =>
        CompetitionStages.For(format, scoreMode)
            .Select(kind => new CategoryStage
            {
                Id = Guid.NewGuid(),
                OrgId = category.OrgId,
                CategoryId = category.Id,
                Kind = kind,
                QualifiersPerGroup = kind is StageKind.Groups or StageKind.Classification
                    ? category.QualifiersPerGroup
                    : null,
                UsesRepechage = kind == StageKind.Bracket && category.UsesRepechage,
            })
            .ToList();
}
