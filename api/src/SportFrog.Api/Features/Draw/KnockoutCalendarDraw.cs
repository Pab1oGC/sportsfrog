using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// A knockout can only draw its first round, because who plays the second is
/// not known until the first is played.
/// </summary>
internal sealed class KnockoutCalendarDraw : ICalendarDraw
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
        var (matches, byes) = Bracket.FirstRound([.. teams.Select(team => team.Id)]);

        return (matches, Bracket.Phase(matches.Count, 1), byes.Count);
    }
}
