namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Binds a user to an organization with one role. The single authority on
/// who belongs where and with which permissions (RF-01, RF-02).
///
/// Carries org_id and is therefore subject to the isolation policy: reading
/// memberships already requires an established organization context.
/// </summary>
public sealed class OrganizationMembership
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid UserId { get; set; }

    public MembershipRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public User? User { get; set; }
}
