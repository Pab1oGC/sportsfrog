namespace SportFrog.Domain.Rules;

/// <summary>
/// What a <see cref="Ruleset"/> actually says: how a match is divided, what
/// winning is worth, how a tie in the standings is broken, and which events
/// may be recorded.
///
/// Stored as jsonb and typed here. The shape is not free-form — the schema's
/// CHECK constraint guarantees the three required keys exist and nothing
/// more, which is deliberate: a constraint cannot know that a volleyball
/// ruleset must price set outcomes rather than draws, or that the metrics
/// named must belong to the sport being played. Those are decided against the
/// catalog, in <see cref="Rulebook.RulesetPolicy"/>.
/// </summary>
/// <remarks>
/// jsonb rather than columns because the answer differs by sport and would
/// otherwise be a wide table of mostly-null fields, one column per rule any
/// sport ever needed. What is common across sports — which sport, whose it
/// is, what it is called — is columns; what varies is here.
/// </remarks>
public sealed record RulesetConfiguration
{
    public required PeriodRules Periods { get; init; }

    /// <summary>
    /// What each match outcome is worth in the standings, keyed by outcome.
    /// </summary>
    /// <remarks>
    /// A dictionary and not a record with fixed members, because the set of
    /// outcomes is a property of the sport. A football match ends won, drawn
    /// or lost; a volleyball match ends 3-0, 3-1, 3-2 or the mirror of those,
    /// and leagues price those differently — taking a set off the winner is
    /// worth something. Which keys are required is derived from the sport in
    /// <see cref="Rulebook.MatchOutcomes"/> and checked before the ruleset is
    /// stored, so the freedom here is in the shape and not in what is
    /// accepted.
    /// </remarks>
    public required IReadOnlyDictionary<string, int> Points { get; init; }

    /// <summary>
    /// How teams level on points are separated, applied in order.
    /// </summary>
    /// <remarks>
    /// The order is the rule. Two leagues can hold the same three criteria
    /// and produce different tables from the same results, so this is a list
    /// and never a set.
    /// </remarks>
    public required IReadOnlyList<string> Tiebreakers { get; init; }

    /// <summary>
    /// The score a match is recorded with when a team does not appear.
    /// </summary>
    /// <remarks>
    /// Optional: a competition may prefer to leave the result to whoever runs
    /// it rather than fix it in advance.
    /// </remarks>
    public WalkoverRules? Walkover { get; init; }

    /// <summary>
    /// Which of the sport's metrics may be recorded, by code.
    /// </summary>
    /// <remarks>
    /// Absent means all of them. A league that does not track assists says so
    /// by listing what it does track; one that tracks everything the sport
    /// offers should not have to enumerate it to say nothing special.
    /// </remarks>
    public IReadOnlyList<string>? Metrics { get; init; }
}

/// <summary>How a match is divided, and for how long.</summary>
public sealed record PeriodRules
{
    public required short Count { get; init; }

    /// <summary>
    /// What a period is called here. Defaulted from the sport, overridable:
    /// the same sport is played in halves in one league and in quarters in
    /// another.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Length of a period. Null where the period ends on a score rather than
    /// on a clock, which is every sport played in sets.
    /// </summary>
    public short? Minutes { get; init; }
}

/// <summary>The score awarded when a match is not played.</summary>
public sealed record WalkoverRules
{
    public required short WinnerScore { get; init; }

    public required short LoserScore { get; init; }
}
