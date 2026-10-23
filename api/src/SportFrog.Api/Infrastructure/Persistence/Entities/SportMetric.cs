namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Something worth recording during a match of a given sport: a goal, a
/// rebound, a yellow card.
///
/// Part of the shared catalog, for the same reason as <see cref="Sport"/>:
/// which events a sport has is a property of the sport, not a preference of
/// the organization playing it.
/// </summary>
public sealed class SportMetric
{
    public Guid Id { get; set; }

    public required string SportCode { get; set; }

    /// <summary>Identifier within the sport. Unique together with the sport.</summary>
    public required string Code { get; set; }

    public required string Label { get; set; }

    /// <summary>
    /// Whether recording this event moves the score.
    /// </summary>
    /// <remarks>
    /// True for a goal, false for an assist. Not a property of
    /// <see cref="ScoreMode.Sets"/> itself: under it the match result always
    /// comes from the periods won, never from an event summed directly, but
    /// an event can still decide which side takes the period being played
    /// right now — a taekwondo kyorugi point or gam-jeom, a volleyball
    /// point, ace, block_point or error, all carry this. A wally point does
    /// not: it is counted as a statistic and never added to the match
    /// result.
    /// </remarks>
    public bool AffectsScore { get; set; }

    /// <summary>
    /// How many points recording this metric once (quantity 1) is worth
    /// toward the match score. Meaningless where <see cref="AffectsScore"/>
    /// is false — a football goal and a basketball free throw are both worth
    /// one, a field goal two, a three-pointer three.
    /// </summary>
    public int ScorePoints { get; set; } = 1;

    /// <summary>
    /// True only for an own goal: the points it is worth belong to the other
    /// team, not to the team the roster entry recording it plays for.
    /// </summary>
    public bool CountsForOpponent { get; set; }

    /// <summary>Whether the metric can head a leaderboard — top scorer, most assists.</summary>
    public bool IsRankable { get; set; }

    public short DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Sport? Sport { get; set; }
}
