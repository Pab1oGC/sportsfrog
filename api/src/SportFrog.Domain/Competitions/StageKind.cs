namespace SportFrog.Domain.Competitions;

/// <summary>
/// One leg of a competition's draw: a round robin table, a group stage, a
/// knockout bracket, or a classification round of individual performances.
/// </summary>
/// <remarks>
/// Not a free choice — see <see cref="CompetitionStages"/>, which derives the
/// stages a competition actually has from its own <see cref="CompetitionFormat"/>
/// and the sport's <see cref="Rules.ScoreMode"/>. A stage is where a
/// reglamento, a set of enabled pitches, or a group's qualifiers finally
/// attach to something narrower than the whole competition.
/// </remarks>
public enum StageKind
{
    /// <summary>Everyone plays everyone, once. The only stage a league format has.</summary>
    League,

    /// <summary>
    /// A group stage proper — a round-robin table per group, ranked by points.
    /// What a <see cref="CompetitionFormat.Groups"/> competition runs first,
    /// unless the sport is <see cref="Rules.ScoreMode.Judged"/>, which runs
    /// <see cref="Classification"/> instead.
    /// </summary>
    Groups,

    /// <summary>
    /// Every team performs once for judges, nobody paired against anybody —
    /// a group stage's equivalent for a sport with no result to tabulate
    /// into a table. What a <see cref="CompetitionFormat.Groups"/>
    /// competition runs first for a <see cref="Rules.ScoreMode.Judged"/>
    /// sport, in place of <see cref="Groups"/>.
    /// </summary>
    Classification,

    /// <summary>
    /// Single elimination. The only stage a knockout format has on its own,
    /// and what a groups format moves on to once its first stage closes.
    /// </summary>
    Bracket,
}
