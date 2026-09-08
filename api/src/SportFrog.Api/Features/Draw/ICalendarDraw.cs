using SportFrog.Domain.Competitions;

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
