using System.Text.Json.Serialization;

namespace SportFrog.Domain.Matches;

/// <summary>
/// What each side scored in one period of a match.
/// </summary>
/// <remarks>
/// Stored inside the match rather than as rows of its own, and the schema
/// says why: periods are never read or written apart from the match they
/// belong to, and how many there are varies by sport. A table would buy joins
/// and a foreign key for something that is always fetched whole.
///
/// The keys are one letter each because the schema's documented shape is
/// <c>[{"p":1,"h":2,"a":1}]</c> and hundreds of these are stored per season.
/// They are named here so the C# reads as English while the column keeps the
/// form everything else expects.
///
/// <see cref="Home"/> and <see cref="Away"/> are whole numbers for every
/// mode, including <see cref="SportFrog.Domain.Rules.ScoreMode.Judged"/>,
/// where a judge's real score has two decimal places. Rather than widening a
/// type every sport shares to carry the one mode that needs fractions, a
/// judged score is recorded ×100 — 7.65 points is stored as 765. The scale
/// is a presentation fact, not a domain one: nothing here divides it back
/// down, because nothing in this layer needs the number for arithmetic
/// beyond comparing which side's is larger.
/// </remarks>
public sealed record PeriodScore
{
    /// <summary>Which period this is, counting from one.</summary>
    [JsonPropertyName("p")]
    public required short Period { get; init; }

    [JsonPropertyName("h")]
    public required int Home { get; init; }

    [JsonPropertyName("a")]
    public required int Away { get; init; }
}
