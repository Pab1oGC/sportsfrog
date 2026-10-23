namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// What varies by stage for one category: the reglamento it is played
/// under, and whatever only means something at that point of the draw.
/// </summary>
/// <remarks>
/// A volleyball league that plays its group stage to three sets and its
/// final to five needs a reglamento per stage, not one for the whole
/// category — and today it has no way to say that at all: the only override
/// is <see cref="Category.RulesetId"/>, one per category, and
/// <see cref="Rulebook.RulesetUsage"/> already refuses to touch a reglamento
/// once anything has been played under it, which is correct but leaves no
/// room to declare the later stage's rules ahead of time without rewriting
/// the earlier one's.
///
/// Not yet read by anything. <see cref="RulesetId"/> exists so a later
/// change can resolve it — category's own, this stage's own where it is set,
/// the competition's otherwise — without a second migration; until that
/// change lands it stays null on every row the backfill created, and
/// <see cref="Rulebook.EffectiveRuleset"/> stays the two-level answer it is
/// today.
///
/// <see cref="QualifiersPerGroup"/>, <see cref="KnockoutEntrants"/> and
/// <see cref="UsesRepechage"/> are copied here from the matching columns on
/// <see cref="Category"/> by the same migration, onto whichever stage of
/// that category they actually describe — a group stage's qualifier count,
/// a bracket's entrants and repechage. The columns on <see cref="Category"/>
/// are not removed yet: moving every reader of them onto this table is
/// later work, kept separate from adding the table at all.
/// </remarks>
public sealed class CategoryStage
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CategoryId { get; set; }

    public StageKind Kind { get; set; }

    /// <summary>
    /// This stage's own reglamento, overriding the category's. Null means
    /// the category's own answer applies — see <see cref="Rulebook.EffectiveRuleset"/>.
    /// </summary>
    public Guid? RulesetId { get; set; }

    /// <summary>
    /// How many of this group advance, where this is a <see cref="StageKind.Groups"/>
    /// or <see cref="StageKind.Classification"/> stage. Null elsewhere, and
    /// null here too where the count was never declared.
    /// </summary>
    public short? QualifiersPerGroup { get; set; }

    /// <summary>
    /// Who qualified into this bracket, in seeded order, where this is a
    /// <see cref="StageKind.Bracket"/> stage drawn from an earlier one. Null
    /// everywhere else, and null here too before it has been drawn.
    /// </summary>
    public Guid[]? KnockoutEntrants { get; set; }

    /// <summary>
    /// Whether this bracket settles third place by repechage. Only means
    /// something on a <see cref="StageKind.Bracket"/> row.
    /// </summary>
    public bool UsesRepechage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Category? Category { get; set; }
    public Ruleset? Ruleset { get; set; }
}
