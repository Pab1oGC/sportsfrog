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
    ///
    /// Empty is also accepted, and means something different from "not
    /// filled in yet": a competition drawn as a straight knockout never
    /// builds a standings table, so nothing here is ever read for it. A
    /// ruleset outlives any one competition — the same one is reused by
    /// others that may draw it differently — so whether a table will ever
    /// exist cannot be decided while the ruleset itself is being written.
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
    /// Length of a period. Null where the period does not run on a clock at
    /// all — a volleyball set, decided purely on score. Not the same
    /// question as whether the sport is played in sets: a taekwondo asalto
    /// is also decided by periods won, but it still runs a clock, so this is
    /// filled in for it the same as for a sport scored cumulatively.
    /// </summary>
    public short? Minutes { get; init; }

    /// <summary>
    /// Rest between one period and the next, in minutes.
    /// </summary>
    /// <remarks>
    /// Only means something alongside <see cref="Minutes"/> — a period with
    /// no clock has no time between periods to measure either. Null is "not
    /// declared" rather than "none": <see cref="MatchDuration.From"/> reads
    /// it as zero, so a reglamento written before this existed still adds up
    /// to exactly the total it always did.
    /// </remarks>
    public short? BreakMinutes { get; init; }

    /// <summary>
    /// How long a match under this reglamento is expected to take, declared
    /// rather than computed.
    /// </summary>
    /// <remarks>
    /// Only means something where <see cref="Minutes"/> is null: a sport with
    /// no clock at all — a volleyball set, decided purely on score — has
    /// nothing for <see cref="MatchDuration.From"/> to add up on its own, so
    /// this is the only source of that figure it has. Lives beside
    /// <see cref="Minutes"/> rather than on the competition that plays under
    /// this reglamento, for the same reason the period length itself does: a
    /// youth category can be given a shorter reglamento the same way it can
    /// be given shorter halves (see <c>Category.RulesetId</c>), which a
    /// single competition-wide number never let it do.
    /// </remarks>
    public short? EstimatedMinutes { get; init; }
}

/// <summary>The score awarded when a match is not played.</summary>
public sealed record WalkoverRules
{
    public required short WinnerScore { get; init; }

    public required short LoserScore { get; init; }
}
