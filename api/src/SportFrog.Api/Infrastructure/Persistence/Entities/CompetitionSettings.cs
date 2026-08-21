namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Settings of a competition that do not deserve a column each.
/// </summary>
/// <remarks>
/// The schema documents two sections. Only <see cref="Public"/> is modelled
/// here; the scheduling section belongs to the module that generates
/// fixtures, and its entries name venue spaces that have no table row yet —
/// writing a type for it now would be writing validation for something
/// nothing can produce.
///
/// That module adds its section to this record. Until it does, a competition
/// stores only what this describes, so nothing can be lost by the omission.
/// </remarks>
public sealed record CompetitionSettings
{
    public PublicSettings? Public { get; init; }
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
