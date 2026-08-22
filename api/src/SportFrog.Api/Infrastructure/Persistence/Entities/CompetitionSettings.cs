namespace SportFrog.Api.Infrastructure.Persistence.Entities;

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
/// Standings and leaders default to shown, because a published competition
/// with neither has nothing to publish. Rosters default to hidden: naming the
/// children on a team is a decision someone has to make on purpose.
/// </remarks>
public sealed record PublicSettings
{
    public bool ShowStandings { get; init; } = true;

    public bool ShowLeaders { get; init; } = true;

    public bool ShowRosters { get; init; }
}
