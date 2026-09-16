using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Competitions.Bulletin;

/// <summary>
/// Spanish wording for the five tiebreaker codes <c>SportFrog.Domain.Rules.Tiebreaker</c>
/// defines, worded for the sport that is actually playing them.
/// </summary>
/// <remarks>
/// A port of the frontend's own <c>lib/tiebreaker-labels.js</c> — kept as two
/// copies rather than one shared source because the two run in different
/// languages, not because the wording is meant to drift. If one changes, so
/// should the other: a tiebreaker in this bulletin has to read the same as
/// the same tiebreaker on the public standings page.
/// </remarks>
internal static class TiebreakerLabels
{
    private static readonly Dictionary<string, string> KnownPlurals = new() { ["gol"] = "goles" };

    public static IReadOnlyDictionary<string, string> For(Sport sport)
    {
        var playedInSets = sport.ScoreMode == ScoreMode.Sets;
        var singular = playedInSets
            ? (sport.PeriodLabel.Length > 0 ? sport.PeriodLabel : "set")
            : (sport.ScoringUnit.Length > 0 ? sport.ScoringUnit : "punto");
        var unit = Plural(singular.ToLowerInvariant());
        var (favor, against) = playedInSets ? ("ganados", "perdidos") : ("a favor", "en contra");

        return new Dictionary<string, string>
        {
            ["score_difference"] = $"Diferencia de {unit}",
            ["score_for"] = $"{Capitalize(unit)} {favor}",
            ["score_against"] = $"{Capitalize(unit)} {against}",
            ["wins"] = "Partidos ganados",
            ["head_to_head"] = "Enfrentamiento directo",
        };
    }

    private static string Plural(string word) =>
        KnownPlurals.TryGetValue(word, out var known) ? known : word.EndsWith('s') ? word : $"{word}s";

    private static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
