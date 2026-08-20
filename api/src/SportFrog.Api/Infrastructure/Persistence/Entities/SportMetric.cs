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
    /// True for a goal, false for an assist. Under
    /// <see cref="ScoreMode.Sets"/> no metric carries it: the score comes from
    /// the periods won, so a volleyball point is counted as a statistic and
    /// never added to the match result.
    /// </remarks>
    public bool AffectsScore { get; set; }

    /// <summary>Whether the metric can head a leaderboard — top scorer, most assists.</summary>
    public bool IsRankable { get; set; }

    public short DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Sport? Sport { get; set; }
}
