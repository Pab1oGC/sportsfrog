namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// An organizer's account on the platform, with its own data isolated from
/// every other organization (RF-01).
///
/// This table carries no row-level security policy: it has no org_id, and
/// the public path must resolve it by slug *before* an isolation context can
/// be established (DD-08).
/// </summary>
public sealed class Organization
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>First segment of the public URL. Case-insensitive (citext).</summary>
    public required string Slug { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>Reserved for future billing. No functional effect in v1.</summary>
    public string Plan { get; set; } = "free";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = [];
}
