using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Draw;

/// <summary>Everyone plays everyone, drawn all at once.</summary>
internal sealed class LeagueCalendarDraw : ICalendarDraw
{
    public string Format => CompetitionFormat.League;

    public string? Inspect(IReadOnlyList<DrawnTeam> teams, int legs) => null;

    public (IReadOnlyList<DrawnMatch> Matches, string? Phase, int Byes) Draw(
        IReadOnlyList<DrawnTeam> teams, int legs) =>
        (RoundRobin.Draw([.. teams.Select(team => team.Id)], legs), null, 0);
}
