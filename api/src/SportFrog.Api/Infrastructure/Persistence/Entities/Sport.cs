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

    /// <summary>
    /// Whether one period of this sport is timed at all. Independent of
    /// <see cref="ScoreMode"/>: a volleyball set and a taekwondo asalto are
    /// both decided by <see cref="Rules.ScoreMode.Sets"/>, but a set has no
    /// clock and an asalto runs two minutes on one — "how the result of a
    /// period is decided" and "whether it can also run out of time" are two
    /// different facts about a sport.
    /// </summary>
    public bool PeriodHasClock { get; set; } = true;

    /// <summary>
    /// The standard clock length of one period, in minutes — a reglamento
    /// form's starting point, not a rule anyone is held to. Null exactly
    /// where <see cref="PeriodHasClock"/> is false: there is no standard
    /// length to suggest for a period that does not run on a clock.
    /// </summary>
    public short? DefaultMinutes { get; set; }

    /// <summary>
    /// The standard rest between periods, in minutes — a reglamento form's
    /// starting point, same as <see cref="DefaultMinutes"/>. Null exactly
    /// where that one is null: no clock, no rest between periods to suggest.
    /// </summary>
    public short? DefaultBreakMinutes { get; set; }

    /// <summary>What one unit of score is called: goal, point.</summary>
    public required string ScoringUnit { get; set; }

    public ScoreMode ScoreMode { get; set; }

    /// <summary>
    /// How many points decide one period under <see cref="Rules.ScoreMode.Sets"/>,
    /// for a sport whose periods are a race to a point target rather than a
    /// fixed stretch of a clock. Null for every other sport, including one
    /// also decided in sets: a taekwondo asalto runs to the end of its two
    /// minutes and whoever has more points at that moment wins it — there is
    /// no number it races to.
    /// </summary>
    /// <remarks>
    /// Not a margin by itself — a set won 25-24 has not actually been won.
    /// FIVB's rule is this many points with at least a two-point lead,
    /// continuing past it until one side has one, and
    /// <see cref="Features.Matches.SetsResultShape"/> is where that margin is
    /// read. It is not a second column here: every sport this one will ever
    /// describe shares the same "win by two" shape, so stating it once as a
    /// constant said more than a column only one value would ever go in.
    /// </remarks>
    public short? TargetPointsPerSet { get; set; }

    /// <summary>
    /// The target for the last period a match in sets can reach, where it
    /// differs from every other one — 15 for volleyball's fifth set, where
    /// <see cref="TargetPointsPerSet"/> is 25.
    /// </summary>
    /// <remarks>
    /// Null means it does not differ, read as <see cref="TargetPointsPerSet"/>
    /// for the deciding period too, rather than every sport with a single
    /// target having to repeat it as both.
    /// </remarks>
    public short? TargetPointsDecidingSet { get; set; }

    /// <summary>
    /// True for a sport whose entrant is one athlete rather than a squad — a
    /// team of one, still fielded through the same <see cref="Team"/>/
    /// <see cref="RosterEntry"/> tables, with its delegation carried by the
    /// team's club.
    /// </summary>
    public bool IsIndividual { get; set; }

    /// <summary>
    /// How many athletes may make up one entry in this sport.
    /// </summary>
    /// <remarks>
    /// Not the same question as <see cref="IsIndividual"/>, which only says
    /// that the unit entering is a person rather than a club. Kyorugi is
    /// fought one against one and a pair cannot exist; Poomsae runs
    /// individual, pair and trio under that same flag — see
    /// <c>IndividualTeamName</c>, which already reads naturally for all
    /// three.
    ///
    /// Null for a team sport: there is no ceiling the sport itself imposes,
    /// the squad size is the category's business. Where both are set, the cap
    /// that applies is the lower of this and
    /// <see cref="Category.MaxRosterSize"/> — a Poomsae category for pairs
    /// narrows the sport's trio ceiling to two, and nothing a category says
    /// can widen it past the sport.
    /// </remarks>
    public short? MaxEntrySize { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<SportMetric> Metrics { get; set; } = [];
}
