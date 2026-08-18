namespace SportFrog.Domain.ValueObjects;

/// <summary>
/// An email address that has been checked for shape. A user is created with
/// one, and a malformed address must be rejected before it ever reaches the
/// database (RF-02).
///
/// The check is deliberately structural, not exhaustive: it rules out
/// addresses that cannot possibly be delivered to — no "@", no local part, a
/// domain with nowhere to route to — and leaves the rest to the mail server.
/// Trying to validate an address fully in code is a known dead end.
/// </summary>
public sealed class Email : IEquatable<Email>
{
    private Email(string value) => Value = value;

    /// <summary>The address exactly as it was given, without trimming or casing changes.</summary>
    public string Value { get; }

    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    /// <exception cref="FormatException">The value is not a well-formed address.</exception>
    public static Email Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return IsWellFormed(value)
            ? new Email(value)
            : throw new FormatException($"'{value}' is not a well-formed email address.");
    }

    /// <summary>
    /// Non-throwing counterpart of <see cref="Parse"/>, for the paths that
    /// report the problem rather than interrupt the operation.
    /// </summary>
    public static bool TryParse(string? value, out Email? email)
    {
        if (value is not null && IsWellFormed(value))
        {
            email = new Email(value);
            return true;
        }

        email = null;
        return false;
    }

    private static bool IsWellFormed(string value)
    {
        // Whitespace anywhere disqualifies the address, which is what rules
        // out both "usuario dominio.com" and an otherwise valid address with
        // text trailing after it.
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = value.Split('@');
        if (parts.Length != 2)
        {
            return false;
        }

        var (localPart, domain) = (parts[0], parts[1]);
        if (localPart.Length == 0 || domain.Length == 0)
        {
            return false;
        }

        // A domain with no dot has no top-level label, so there is nowhere to
        // route the message. This is what rejects "usuario@com".
        var labels = domain.Split('.');
        return labels.Length >= 2 && labels.All(label => label.Length > 0);
    }

    public bool Equals(Email? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Email);

    // Case-insensitive, matching the citext column the address is stored in:
    // two addresses that the database considers the same must not look
    // different here.
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
