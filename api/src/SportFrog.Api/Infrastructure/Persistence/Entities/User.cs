namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A person who signs in. Users are platform-wide, not owned by an
/// organization: the link to each organization is an
/// <see cref="OrganizationMembership"/>, so one account can belong to
/// several leagues with different roles (RF-02).
///
/// Deleting is always soft: the user is referenced as the author of results,
/// documents and audit entries.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>Case-insensitive (citext) and unique across the platform.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// Output of a key derivation function with a configurable cost factor.
    /// Never a general-purpose digest, never plaintext.
    /// </summary>
    public required string PasswordHash { get; set; }

    public required string FullName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = [];
}
