namespace SportFrog.Domain.Competitions;

/// <summary>
/// Settings of a competition that do not deserve a column each.
/// </summary>
/// <remarks>
/// The schema documents two sections and both are here. <see cref="Public"/>
/// decides what a visitor is shown; <see cref="Schedule"/> says when and
/// where the competition can be played, which is what turns a draw into a
/// calendar.
/// </remarks>
public sealed record CompetitionSettings
{
    public PublicSettings? Public { get; init; }

    public ScheduleSettings? Schedule { get; init; }
}

/// <summary>
/// When and where this competition can put a fixture.
/// </summary>
/// <remarks>
/// Availability rather than a calendar: it describes the windows an
/// organization has — Saturdays and Sundays, eight to two, on these three
/// pitches — and the placement works out what fits. Writing the calendar
/// itself here would mean editing settings every time a fixture moves.
/// </remarks>
public sealed record ScheduleSettings
{
    /// <summary>
    /// How long a fixture occupies its pitch, including whatever the
    /// organizers leave between matches.
    /// </summary>
    /// <remarks>
    /// One number rather than a duration per sport, because what is being
    /// booked is the ground: a futsal match and a football one both take the
    /// slot the organization hands out, and the slot is what the next team
    /// waits for.
    /// </remarks>
    public short SlotMinutes { get; init; }

    public IReadOnlyList<ScheduleSpace>? Spaces { get; init; }
}

/// <summary>One pitch, and the hours it can be used.</summary>
public sealed record ScheduleSpace
{
    public Guid VenueSpaceId { get; init; }

    /// <summary>
    /// Days of the week it is available, Sunday being zero.
    /// </summary>
    /// <remarks>
    /// Matches <see cref="DayOfWeek"/>, so the documented <c>[6, 0]</c> reads
    /// as Saturday and Sunday — the weekend, which is when amateur sport is
    /// played. Absent or empty means every day.
    /// </remarks>
    public IReadOnlyList<int>? Days { get; init; }

    /// <summary>First kick-off of the day, as "08:00".</summary>
    public string? From { get; init; }

    /// <summary>
    /// The last moment a fixture may still start.
    /// </summary>
    /// <remarks>
    /// A start time and not a closing time: a slot beginning at the boundary
    /// is allowed, and how long it then runs is the slot's business. Reading
    /// it the other way would silently drop the last fixture of every day.
    /// </remarks>
    public string? To { get; init; }
}

/// <summary>
/// What a visitor sees on the public page, once the competition is published
/// at all.
/// </summary>
/// <remarks>
/// Separate from the competition's own <c>is_public</c>, which is the switch:
/// that decides whether there is a public page, these decide what is on it.
/// The two are not redundant — a competition can be public while its rosters
/// are not, and that combination is the common one where minors play (RNF-16).
///
/// Standings, leaders and classification default to shown, because a
/// published competition with none of them has nothing to publish. Rosters
/// default to hidden: naming the children on a team is a decision someone
/// has to make on purpose.
/// </remarks>
public sealed record PublicSettings
{
    public bool ShowStandings { get; init; } = true;

    public bool ShowLeaders { get; init; } = true;

    /// <summary>
    /// Whether a judged category's classification stage — poomsae's ranking
    /// by score, before its knockout is drawn — appears on the public page.
    /// </summary>
    /// <remarks>
    /// Its own switch rather than folded into <see cref="ShowStandings"/>:
    /// the two never coexist on the same category (one sport is scored by
    /// table, the other by judges), but a competition can run both kinds of
    /// category at once, and an organizer publishing one division's table
    /// should not be assumed to also want another division's judges' scores
    /// public before they are final.
    /// </remarks>
    public bool ShowClassification { get; init; } = true;

    public bool ShowRosters { get; init; }

    /// <summary>
    /// The cover image behind the competition's name, as a storage key —
    /// never a URL. Absent means the plain brand background every
    /// competition had before this existed.
    /// </summary>
    public string? BannerKey { get; init; }

    /// <summary>The competition's own color for its public page, as "#rrggbb".</summary>
    public string? AccentColor { get; init; }

    /// <summary>A line or two under the name: who runs this, what it is for.</summary>
    public string? Description { get; init; }

    public string? Instagram { get; init; }

    public string? Facebook { get; init; }

    public string? WhatsApp { get; init; }

    public string? Website { get; init; }

    /// <summary>Who paid to appear on the page, in the order they appear.</summary>
    public IReadOnlyList<SponsorLink>? Sponsors { get; init; }
}

/// <summary>One name in the strip of sponsors a public page may show.</summary>
public sealed record SponsorLink
{
    /// <summary>The mark itself, as a storage key.</summary>
    public required string LogoKey { get; init; }

    public string? Name { get; init; }

    /// <summary>Where the mark links to, if it should be clickable at all.</summary>
    public string? Url { get; init; }
}
