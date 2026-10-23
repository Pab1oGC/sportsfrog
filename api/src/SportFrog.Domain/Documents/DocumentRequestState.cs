using System.Text.Json.Serialization;

namespace SportFrog.Domain.Documents;

/// <summary>Where a request to issue a document has got to.</summary>
/// <remarks>
/// A credential or a certificate is printed by SportFrog itself, never by
/// the organization it is about — see <c>PlatformAuthority</c>. This is the
/// other half of that rule: an organization that cannot print still has to
/// be able to ask, and this names what happens to that ask, regardless of
/// which kind it was for.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<DocumentRequestState>))]
public enum DocumentRequestState
{
    /// <summary>Asked for, and not yet looked at.</summary>
    Pending,

    /// <summary>Looked at and accepted — a batch was raised to print it.</summary>
    Approved,

    /// <summary>Looked at and refused.</summary>
    Rejected,

    /// <summary>Withdrawn by the organization that asked, before anyone looked at it.</summary>
    Cancelled,
}
