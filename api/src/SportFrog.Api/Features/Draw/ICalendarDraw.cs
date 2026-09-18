using SportFrog.Domain.Competitions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Features.Draw;

/// <summary>One team as the draw sees it: who it is, and which group it was placed in.</summary>
internal sealed record DrawnTeam(Guid Id, string? GroupLabel);

/// <summary>
/// One format's way of drawing a category's fixtures from the teams still
/// competing in it.
/// </summary>
/// <remarks>
/// The seam <see cref="DrawCalendar"/> used to branch on internally with a
/// switch over <see cref="CompetitionFormat"/>, made into something a new
/// format can implement instead of a case <see cref="DrawCalendar"/> would
/// otherwise need editing to add — the same shape as
/// <c>IMatchOutcomeRules</c>, <c>IRulesetShapeRules</c> and
/// <c>IResultShapeRules</c>, resolved through <see cref="ICalendarDrawRegistry"/>
/// rather than a mode check.
/// </remarks>
internal interface ICalendarDraw
{
    /// <summary>
    /// The format this draws — the key <see cref="ICalendarDrawRegistry"/>
    /// resolves it by. One of the constants on <see cref="CompetitionFormat"/>.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// The one reason this format cannot be drawn from these teams under
    /// this request, or null. Checked once every format shares — at least
    /// two teams still competing — has already passed.
    /// </summary>
    string? Inspect(IReadOnlyList<DrawnTeam> teams, int legs);

    /// <summary>The fixtures, named phase, and byes this format produces from these teams.</summary>
    (IReadOnlyList<DrawnMatch> Matches, string? Phase, int Byes) Draw(IReadOnlyList<DrawnTeam> teams, int legs);
}

/// <summary>
/// A format whose calendar can be drawn whole — every round's fixtures at
/// once, not only the first.
/// </summary>
/// <remarks>
/// Only a pure knockout implements this. League and Groups already draw
/// everything they will ever draw in one call to <see cref="ICalendarDraw.Draw"/> —
/// a league is not played a round at a time to begin with. A knockout
/// promoted from a group stage still advances one round at a time through
/// <c>AdvanceBracket</c>, because its entrants are not known until the group
/// stage decides who qualifies; this is only for the case where every
/// entrant is already known the moment the draw runs.
/// </remarks>
internal interface IPlansEntireBracket
{
    /// <summary>Every round of the bracket this field produces, from the first match to the final.</summary>
    IReadOnlyList<PlannedMatch> DrawFull(IReadOnlyList<DrawnTeam> teams);
}
