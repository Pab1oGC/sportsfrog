namespace SportFrog.Domain.ValueObjects;

/// <summary>
/// A password that satisfies the minimum security policy, checked before it
/// ever reaches the hasher (RF-02).
///
/// This type holds the plain text only for as long as it takes to derive it.
/// What gets persisted is the output of the key derivation function, never
/// this value.
/// </summary>
public sealed class Password
{
    /// <summary>Shortest accepted password.</summary>
    public const int MinimumLength = 8;

    private Password(string value) => Value = value;

    /// <summary>The password exactly as it was entered.</summary>
    public string Value { get; }

    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    /// <exception cref="FormatException">The value does not satisfy the policy.</exception>
    public static Password Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return SatisfiesPolicy(value)
            ? new Password(value)
            : throw new FormatException(
                $"The password does not meet the minimum policy: at least {MinimumLength} " +
                "characters, with an uppercase letter, a lowercase letter, a digit and a " +
                "special character.");
    }

    /// <summary>
    /// Non-throwing counterpart of <see cref="Parse"/>, for the paths that
    /// report the problem rather than interrupt the operation.
    /// </summary>
    public static bool TryParse(string? value, out Password? password)
    {
        if (value is not null && SatisfiesPolicy(value))
        {
            password = new Password(value);
            return true;
        }

        password = null;
        return false;
    }

    private static bool SatisfiesPolicy(string value) =>
        value.Length >= MinimumLength
        && value.Any(char.IsUpper)
        && value.Any(char.IsLower)
        && value.Any(char.IsDigit)
        && value.Any(IsSpecial);

    /// <summary>
    /// Whitespace is the absence of a special character, not one of them: a
    /// naive "not a letter or digit" check would accept a space as
    /// satisfying the rule and weaken the policy without anyone noticing.
    /// </summary>
    private static bool IsSpecial(char character) =>
        !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character);

    // Deliberately not the value: a password must not leak into a log line
    // or an exception message through a careless interpolation.
    public override string ToString() => "********";
}
