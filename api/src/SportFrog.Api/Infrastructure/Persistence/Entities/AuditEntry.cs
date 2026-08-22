using System.Net;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One recorded change: who, when, to what, and what it became (RNF-12).
///
/// The table carries no foreign keys on purpose, and the user's address is
/// copied rather than referenced: the record has to outlive whatever it talks
/// about. An entry that stopped naming its author once the account was
/// removed would fail at the one moment it is consulted.
///
/// Append-only, enforced by a trigger that refuses UPDATE and DELETE even
/// from the application.
/// </summary>
public sealed class AuditEntry
{
    public long Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>Copied, not referenced, so it survives the account.</summary>
    public string? UserEmail { get; set; }

    public required string EntityType { get; set; }

    public Guid? EntityId { get; set; }

    /// <summary>create, update or delete.</summary>
    public required string Action { get; set; }

    /// <summary>What changed, as JSON. Never the values of secrets.</summary>
    public string? Changes { get; set; }

    /// <summary>
    /// Why, when the change alone does not say it. Revoking a document and
    /// applying a walkover look like ordinary updates; their meaning is not
    /// recoverable from the columns that moved.
    /// </summary>
    public string? Reason { get; set; }

    public IPAddress? IpAddress { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
