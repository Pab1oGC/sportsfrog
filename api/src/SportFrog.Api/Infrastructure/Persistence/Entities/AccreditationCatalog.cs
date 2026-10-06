namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One thing a credential can name: the discipline, a venue, a service or an
/// access zone.
/// </summary>
/// <remarks>
/// The variable half of a credential that is otherwise fixed by decree. The
/// blocks of the card, their order and their measurements are settled; what
/// goes inside them is these rows, and they belong to one competition.
///
/// Four kinds in one entity because they are one shape — a short code, a name,
/// and a line in the glossary printed on the back of every card. See
/// <see cref="AccreditationItemKind"/> for why the distinction is only about
/// where each one is drawn.
/// </remarks>
public sealed class AccreditationItem
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    public AccreditationItemKind Kind { get; set; }

    /// <summary>
    /// What is printed in the box, and what the glossary explains.
    /// </summary>
    /// <remarks>
    /// Up to four characters, which is not a tidiness rule: the box is a few
    /// millimetres wide, and a code that does not fit it is a card that comes
    /// out wrong rather than a request that comes back refused.
    /// </remarks>
    public required string Code { get; set; }

    /// <summary>What the code means, spelled out in the glossary on the back.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The colour this entry prints as, as "#rrggbb". Null for most items.
    /// </summary>
    /// <remarks>
    /// A venue or a service is printed as its code in whatever box the layout
    /// gives it — this is for a zone, which is drawn as a colour strip rather
    /// than a word, the way the Pan Am accreditation card operating system
    /// prints "Blue" or "Red" zone access directly as a colour band along the
    /// foot of both faces.
    /// </remarks>
    public string? ColorHex { get; set; }

    /// <summary>
    /// A picture printed instead of the code, in object storage.
    /// </summary>
    /// <remarks>
    /// Null is the usual case and means the code is printed as words. This
    /// exists for dining, which every card of this kind draws as cutlery.
    /// </remarks>
    public string? IconKey { get; set; }

    /// <summary>
    /// Where it sits among its own kind, left to right.
    /// </summary>
    /// <remarks>
    /// Kept rather than sorting by code, because the order the boxes are read
    /// in is a decision somebody makes about the card, not an accident of the
    /// alphabet.
    /// </remarks>
    public short DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Competition? Competition { get; set; }
}

/// <summary>
/// The accreditation category printed on a card — <c>Aa</c>, <c>RTb</c> — and
/// the colour its box and footer band are filled with.
/// </summary>
/// <remarks>
/// Not to be confused with <see cref="Category"/>, which is a division of a
/// competition by age, weight or gender. The names are close enough to be
/// worth the warning: a person belongs to one of each, they answer different
/// questions, and only this one reaches the card.
///
/// It carries a package of <see cref="AccreditationItem"/>, which is what
/// makes accrediting four hundred people a matter of choosing a category each
/// rather than granting them a zone at a time.
/// </remarks>
public sealed class AccreditationCategory
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    /// <summary>What is printed in the category box, large.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// The colour of the category box and of the footer band, as "#rrggbb".
    /// </summary>
    /// <remarks>
    /// Checked by the database as well as by the application, because it is
    /// written straight into a drawing instruction rather than compared
    /// against anything.
    /// </remarks>
    public string ColorHex { get; set; } = "#1F3864";

    public short DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Competition? Competition { get; set; }

    public ICollection<AccreditationCategoryItem> Items { get; set; } = [];
}

/// <summary>One entry of the package a category carries.</summary>
/// <remarks>
/// Both ends also name the competition, so the database itself refuses a
/// category that grants a zone belonging to a different one. That check could
/// have lived in the application, where it would hold until the next place
/// that inserts one of these forgets to write it.
/// </remarks>
public sealed class AccreditationCategoryItem
{
    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid ItemId { get; set; }

    public AccreditationCategory? Category { get; set; }

    public AccreditationItem? Item { get; set; }
}
