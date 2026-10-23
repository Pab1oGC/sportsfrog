using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Competitions;

/// <summary>
/// Which stages a competition actually has, derived from how its fixtures
/// are drawn and how its sport is scored.
/// </summary>
/// <remarks>
/// Never a free choice an organizer makes: a competition does not invent a
/// stage, the draw does. <see cref="CompetitionFormat.League"/> only ever
/// runs one table; <see cref="CompetitionFormat.Knockout"/> only ever runs
/// one bracket; <see cref="CompetitionFormat.Groups"/> runs a first stage
/// and then a bracket among those who advance — and which first stage
/// depends on the sport, not on the format: a <see cref="ScoreMode.Judged"/>
/// sport has nothing to tabulate into a table, so it runs a classification
/// round instead of a group stage.
///
/// Written as an exhaustive match over <see cref="CompetitionFormat.All"/>
/// rather than a registry, the same choice <see cref="Rules.MatchOutcomes"/>'s
/// own remarks weigh and reject for a reason that does not apply here: there
/// is no per-sport strategy to resolve, only one branch — Groups — that
/// depends on the score mode at all, and every format this will ever
/// recognize already exists in the catalog <see cref="CompetitionFormat"/>
/// closes over.
/// </remarks>
public static class CompetitionStages
{
    public static IReadOnlyList<StageKind> For(string format, ScoreMode scoreMode) => format switch
    {
        CompetitionFormat.League => [StageKind.League],
        CompetitionFormat.Knockout => [StageKind.Bracket],
        CompetitionFormat.Groups => scoreMode == ScoreMode.Judged
            ? [StageKind.Classification, StageKind.Bracket]
            : [StageKind.Groups, StageKind.Bracket],
        _ => throw new InvalidOperationException(
            $"No hay fases registradas para el formato de competencia '{format}'."),
    };

    /// <summary>
    /// Which of a category's stages one of its own matches belongs to.
    /// </summary>
    /// <remarks>
    /// League and Knockout only ever have the one stage <see cref="For"/>
    /// already gives them, so a match's own <c>Phase</c> label does not
    /// matter for either. Groups is the only format with two, and for a
    /// non-judged sport <c>Phase</c> is already how <c>StandingsQuery</c>
    /// tells them apart — null for a group-stage match, set for a bracket
    /// one, because a bracket's rounds are always named (<c>Bracket.Phase</c>)
    /// and a group stage's matches never are. A judged sport's Groups
    /// category is the exception that rule cannot answer on its own: its
    /// first stage is a classification round, and that round pairs nobody
    /// and is never recorded as a <c>Match</c> at all — see
    /// <c>OpenClassificationStage</c> — so every <c>Match</c> it ever has
    /// belongs to its bracket regardless of what <c>Phase</c> says.
    /// </remarks>
    public static StageKind KindOfMatch(string format, ScoreMode scoreMode, string? phase)
    {
        var stages = For(format, scoreMode);

        if (stages.Count == 1)
        {
            return stages[0];
        }

        // The first stage of a judged Groups category is Classification,
        // and that stage never produces a Match at all -- a classification
        // round pairs nobody and is recorded as a Performance instead (see
        // OpenClassificationStage). Every Match that category ever has
        // belongs to its bracket, whatever Phase says.
        if (stages[0] == StageKind.Classification)
        {
            return StageKind.Bracket;
        }

        return phase is null ? stages[0] : stages[1];
    }
}
