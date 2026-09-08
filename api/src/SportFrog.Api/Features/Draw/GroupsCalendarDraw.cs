using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// A league inside each group, drawn together so that round one means the
/// same weekend everywhere.
/// </summary>
internal sealed class GroupsCalendarDraw : ICalendarDraw
{
    public string Format => CompetitionFormat.Groups;

    /// <remarks>
    /// A group stage with no groups is a league that has not been drawn into
    /// them yet. Refused rather than quietly treated as one, because the
    /// difference is a decision somebody has to make.
    /// </remarks>
    public string? Inspect(IReadOnlyList<DrawnTeam> teams, int legs) =>
        teams.All(team => team.GroupLabel is null)
            ? "Ningún equipo fue sorteado en un grupo. Definí el grupo de cada equipo antes de "
                + "sortear una fase de grupos."
            : null;

    public (IReadOnlyList<DrawnMatch> Matches, string? Phase, int Byes) Draw(
        IReadOnlyList<DrawnTeam> teams, int legs) =>
        (
            [.. teams
                .GroupBy(team => team.GroupLabel)
                .SelectMany(group => RoundRobin.Draw([.. group.Select(team => team.Id)], legs))],
            null,
            0
        );
}
