using System.Text.Json.Serialization;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Which kind of target one <see cref="MembershipScope"/> row narrows a
/// membership to.
/// </summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<ScopeType>))]
public enum ScopeType
{
    /// <summary>The membership reaches its role in this one competition.</summary>
    Competition,

    /// <summary>The membership reaches its role for this one club's own data.</summary>
    Club,
}

/// <summary>
/// Narrows a membership's role to one competition or one club, rather than
/// the whole organization.
///
/// A membership with no rows here is unaffected: it reaches its role
/// everywhere in the organization, exactly as the role check works today
/// (RF-02). A membership WITH rows reaches that same role only inside the
/// competitions and clubs those rows name — an event's own administrator,
/// or a club's delegate, are a role held narrower than the organization,
/// not a different or lesser role.
///
/// This is deliberately a second, independent axis from
/// <see cref="MembershipRole"/>'s rank: how much a membership may do and
/// where it may do it are two different questions, and conflating them —
/// giving a club delegate a lower-ranked role to approximate a narrower one
/// — cannot express "full operator authority, but only over this club",
/// which is the actual shape of the roles this table exists for.
/// </summary>
/// <remarks>
/// One row names exactly one target: <see cref="ScopeType"/> says which of
/// <see cref="CompetitionId"/>/<see cref="ClubId"/> must be set, the database
/// enforces that the other is null (ck_membership_scope_target), and a
/// membership that must reach several competitions gets several rows rather
/// than one row widened to cover them.
///
/// Carries org_id and is therefore subject to the isolation policy, like
/// <see cref="OrganizationMembership"/> itself.
/// </remarks>
public sealed class MembershipScope
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid MembershipId { get; set; }

    public ScopeType ScopeType { get; set; }

    public Guid? CompetitionId { get; set; }

    public Guid? ClubId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public OrganizationMembership? Membership { get; set; }
    public Competition? Competition { get; set; }
    public Club? Club { get; set; }
}
