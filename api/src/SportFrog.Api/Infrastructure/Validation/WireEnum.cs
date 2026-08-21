using System.Text.Json;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// The wire form of an enum: how it is written out, how it is read back, and
/// how it is listed to a caller who got it wrong.
/// </summary>
/// <remarks>
/// One place on purpose. These three were separate before, and they drifted
/// immediately: values went out as in_progress because that is what the
/// database calls them, and came back through Enum.TryParse, which has never
/// heard of an underscore. The result was an API that answered a refusal by
/// listing a value it would then refuse — the worst kind of error message,
/// because it sends the caller to look for a mistake they did not make.
///
/// Defined against <see cref="SnakeCaseEnumConverter{TEnum}"/>, which is what
/// actually serializes these, so the reading and the writing cannot disagree
/// again without this file changing.
/// </remarks>
public static class WireEnum
{
    /// <summary>How the value appears in a response.</summary>
    public static string Label<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>
    /// Reads a value written the way responses write it.
    /// </summary>
    /// <remarks>
    /// The separators are dropped before matching rather than the names being
    /// reconstructed: in_progress and InProgress differ only by punctuation
    /// this comparison does not care about, and removing it makes the two
    /// meet in the middle without a lookup table to maintain.
    ///
    /// Case is ignored for the same reason a slug is lowercased — capitals in
    /// a value typed by a person are a typo, not a different value.
    /// </remarks>
    public static bool TryParse<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var name = value.Trim().Replace("_", string.Empty);

        // Enum.TryParse also accepts the underlying number, which is not part
        // of the wire form and is not something a caller should discover
        // works. The labels are the contract; the numbers are an
        // implementation detail that reorders itself the day a member is
        // inserted.
        return !name.All(char.IsAsciiDigit)
            && Enum.TryParse(name, ignoreCase: true, out parsed)
            && Enum.IsDefined(parsed);
    }

    /// <summary>
    /// Reads a value a validator has already accepted.
    /// </summary>
    /// <remarks>
    /// Throws rather than falling back to the first member, which is what
    /// discarding the result of <see cref="TryParse"/> quietly does. A handler
    /// reaching here with an unreadable value means the contract was not
    /// validated, and a competition silently recorded as capturing basic
    /// detail when the caller asked for detailed is a bug that never announces
    /// itself.
    /// </remarks>
    public static TEnum Parse<TEnum>(string? value)
        where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"'{value}' is not a {typeof(TEnum).Name}. Expected one of: {Options<TEnum>()}.",
                nameof(value));

    /// <summary>
    /// Every accepted value, for a refusal message.
    /// </summary>
    public static string Options<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetValues<TEnum>().Select(Label));
}
