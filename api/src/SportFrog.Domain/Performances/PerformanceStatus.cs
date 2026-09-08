using System.Text.Json.Serialization;

namespace SportFrog.Domain.Performances;

/// <summary>What has become of a team's slot in a classification stage.</summary>
/// <remarks>
/// Two states rather than one nullable score, for the same reason
/// <see cref="Matches.MatchState"/> is not a boolean: the row exists the
/// moment the classification stage opens, before anybody has performed, and
/// "no score yet" has to be a state a listing can show rather than an absent
/// value indistinguishable from one nobody entered.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<PerformanceStatus>))]
public enum PerformanceStatus
{
    /// <summary>Entered into the classification stage, not yet performed.</summary>
    Pending,

    /// <summary>Performed and judged. Owes nothing further.</summary>
    Scored,
}
