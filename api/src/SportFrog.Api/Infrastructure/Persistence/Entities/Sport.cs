namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A sport the platform knows how to run.
///
/// Catalog shared by every organization: it carries no org_id, sits outside
/// the isolation policies, and is managed through migrations rather than
/// through the interface. Adding a sport is a change to what the product
/// supports — it needs metrics, scoring rules and a way to consolidate a
/// result — and not something an organization configures for itself.
///
/// What an organization does configure is a <see cref="Ruleset"/>: this says
/// football is played in two halves and scored in goals, that says a
/// particular league plays 45-minute halves and awards three points a win.
/// </summary>
public sealed class Sport
{
    /// <summary>
    /// Stable identifier, and the key. Text rather than a surrogate because
    /// it is referenced by rulesets and competitions and is meaningful when
    /// read: 'football' says what a random identifier would not.
    /// </summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>What one division of a match is called: half, quarter, set.</summary>
    public required string PeriodLabel { get; set; }

    public short DefaultPeriods { get; set; }

    /// <summary>What one unit of score is called: goal, point.</summary>
    public required string ScoringUnit { get; set; }

    public ScoreMode ScoreMode { get; set; }

    /// <summary>
    /// True for a sport whose entrant is one athlete rather than a squad — a
    /// team of one, still fielded through the same <see cref="Team"/>/
    /// <see cref="RosterEntry"/> tables, with its delegation carried by the
    /// team's club.
    /// </summary>
    public bool IsIndividual { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<SportMetric> Metrics { get; set; } = [];
}
