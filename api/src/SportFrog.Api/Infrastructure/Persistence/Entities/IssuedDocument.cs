namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A credential or a certificate that exists in the world.
/// </summary>
/// <remarks>
/// The row is the document. Somebody is carrying a printed card with this
/// serial on it, and a referee at the side of a pitch will scan it — which is
/// why nothing here is ever deleted, physically or logically. A credential
/// that was withdrawn is a fact worth keeping: the answer to "is this card
/// valid" has to be "no, it was revoked", not silence.
///
/// It names the design it was printed from, version and all, so the same card
/// can be produced again years later rather than this season's card with last
/// season's name on it.
/// </remarks>
public sealed class IssuedDocument
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid TemplateId { get; set; }

    /// <summary>Which stored layout it was printed from.</summary>
    public int TemplateVersion { get; set; }

    public DocumentKind Kind { get; set; }

    public Guid CompetitionId { get; set; }

    /// <summary>The person it accredits. Null on a document issued to a team.</summary>
    public Guid? AthleteId { get; set; }

    /// <summary>The team it accredits. Null on a document issued to a person.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>
    /// What is printed on it and what public verification resolves.
    /// </summary>
    /// <remarks>
    /// Unique within the organization, and unguessable on purpose. See
    /// <c>Features.Documents.Serial</c> for why it is not a counter.
    /// </remarks>
    public required string SerialNumber { get; set; }

    /// <summary>Why a certificate was given. Null on a credential.</summary>
    public string? CertificateType { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Where its own PDF lives. Null while a batch is still running.</summary>
    public string? PdfUrl { get; set; }

    public DocumentState Status { get; set; } = DocumentState.Issued;

    public Guid IssuedBy { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public Guid? RevokedBy { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    /// <summary>The batch that printed it, when it came from one.</summary>
    public Guid? BatchId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Athlete? Athlete { get; set; }
    public Team? Team { get; set; }
    public Competition? Competition { get; set; }
}
