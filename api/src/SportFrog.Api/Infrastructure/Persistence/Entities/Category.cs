namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A division within a competition: who may play in it, and under what rules.
///
/// This is the level that actually fields teams. A competition is the event;
/// a category is the draw a club enters, and a match belongs to one. "Sub-15
/// masculino" and "Primera femenina" are two categories of the same league,
/// with their own tables and their own eligibility.
/// </summary>
/// <remarks>
/// Deletion is physical: a category is part of the shape of a competition
/// rather than a record of something that happened, and one with teams in it
/// cannot be removed at all. Teams reference it with ON DELETE RESTRICT, so
/// the database refuses; matches reference it with CASCADE, which only ever
/// applies when the competition above is dropped whole.
/// </remarks>
public sealed class Category
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    /// <summary>
    /// Rules for this division only, replacing the competition's.
    /// </summary>
    /// <remarks>
    /// Null is the ordinary case and means the competition's ruleset applies.
    /// The override exists because divisions of one event genuinely differ:
    /// the youth categories of a league often play shorter halves than the
    /// senior one while everything else stays the same.
    ///
    /// It must be written for the same sport as the competition. Nothing in
    /// the schema says so — the foreign key only requires that the ruleset
    /// exist — so it is checked before the row is written.
    /// </remarks>
    public Guid? RulesetId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Who may be registered, by sex as recorded on the athlete.
    /// </summary>
    /// <remarks>
    /// Null means open: a mixed category, or one where the distinction is not
    /// made. It is an eligibility restriction on a roster and not a statement
    /// about a person, which is why it lives here and not only on the athlete.
    /// </remarks>
    public string? Gender { get; set; }

    /// <summary>Earliest birth date admitted — the oldest player the category takes.</summary>
    public DateOnly? BirthDateFrom { get; set; }

    /// <summary>Latest birth date admitted — the youngest player the category takes.</summary>
    public DateOnly? BirthDateTo { get; set; }

    /// <summary>
    /// How many players a team may register here. Null leaves it uncapped.
    /// </summary>
    public short? MaxRosterSize { get; set; }

    /// <summary>
    /// Where this category sits when they are listed together.
    /// </summary>
    /// <remarks>
    /// Explicit because the natural order is neither alphabetical nor by
    /// creation: organizers list their divisions from youngest to oldest, or
    /// the other way, and no property of the row derives that.
    /// </remarks>
    public short DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Competition? Competition { get; set; }
    public Ruleset? Ruleset { get; set; }
}
