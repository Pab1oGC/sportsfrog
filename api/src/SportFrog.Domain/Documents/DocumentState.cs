using System.Text.Json.Serialization;

namespace SportFrog.Domain.Documents;

/// <summary>Whether a document still counts.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<DocumentState>))]
public enum DocumentState
{
    /// <summary>Printed and valid.</summary>
    Issued,

    /// <summary>Withdrawn. The row stays; it is the record that it was withdrawn.</summary>
    Revoked,
}
