using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// A pure knockout — no group stage ahead of it — whose entrants are all
/// known the moment it is drawn.
/// </summary>
/// <remarks>
/// <see cref="Draw"/> still answers "just round one", the question every
/// other format's own <see cref="ICalendarDraw.Draw"/> answers, kept for
/// whatever still calls it that way. <see cref="DrawFull"/> is what
/// <see cref="DrawCalendar"/> actually uses for this format: every round
/// down to the final, computed by <see cref="Bracket.FullDraw"/> — the one
/// place either method reads the byes and pairings from, so neither can ever
/// disagree with the other about round one.
/// </remarks>
internal sealed class KnockoutCalendarDraw : ICalendarDraw, IPlansEntireBracket
{
    public string Format => CompetitionFormat.Knockout;

    /// <remarks>
    /// A cross decided over two legs is a different object from two
    /// independent results — it is settled on aggregate — so it is refused
    /// here rather than drawn as two ordinary matches that happen to share a
    /// pair of teams.
    /// </remarks>
    public string? Inspect(IReadOnlyList<DrawnTeam> teams, int legs) =>
        legs != 1
            ? "Acá una eliminatoria se sortea a una vuelta. Un cruce a ida y vuelta se decide por "
                + "acumulado, que es un objeto distinto de dos partidos independientes."
            : null;

    public (IReadOnlyList<DrawnMatch> Matches, string? Phase, int Byes) Draw(
        IReadOnlyList<DrawnTeam> teams, int legs)
    {
        var firstRound = DrawFull(teams).Where(match => match.Round == 1).ToList();
        var byeCount = teams.Count - firstRound.Count * 2;

        var matches = firstRound
            .Select(match => new DrawnMatch(1, match.Home.TeamId!.Value, match.Away.TeamId!.Value))
            .ToList();

        return (matches, firstRound.Count > 0 ? firstRound[0].Phase : Bracket.Phase(0, 1), byeCount);
    }

    public IReadOnlyList<PlannedMatch> DrawFull(IReadOnlyList<DrawnTeam> teams) =>
        Bracket.FullDraw([.. teams.Select(team => team.Id)]);
}
