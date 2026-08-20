namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A person registered by an organization (RF-08).
///
/// Identity within the organization is the identity document, not the name:
/// that is what lets a later registration attach to the same person instead
/// of creating a second one, which is what keeps the accumulated history
/// RF-27 promises (RF-43).
///
/// Carries personal data of minors — document, full date of birth, guardian
/// contact. None of it may appear on the public view (RNF-16).
/// </summary>
public sealed class Athlete
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Unique within the organization. The anchor of the person's identity.</summary>
    public required string DocumentId { get; set; }

    public DateOnly BirthDate { get; set; }

    public string? Gender { get; set; }

    /// <summary>
    /// Set once the image has been normalized. The original upload is not
    /// kept, and nothing here is written by these operations yet.
    /// </summary>
    public string? PhotoUrl { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
