using System.Globalization;

namespace SportFrog.Domain.Documents;

/// <summary>
/// The identifier printed in a credential's data block — a delegation code
/// and a number, read by a steward checking a card against a list.
/// </summary>
/// <remarks>
/// Distinct on purpose from the serial the QR carries. <see cref="Serial"/>
/// is sixty bits of randomness precisely so that scanning one card reveals
/// nothing about the next; this is the opposite kind of fact, a sequential
/// number meant to be read aloud and matched against a printed roster, the
/// way an accreditation number works at every games this credential's
/// structure is drawn from. Printing both is printing two different
/// questions' answers, not the same fact twice.
/// </remarks>
public static class VisibleCredentialId
{
    /// <summary>
    /// How many letters of a delegation's code reach the card — a box a few
    /// millimetres wide, the same constraint every other code on this
    /// credential is held to.
    /// </summary>
    private const int CodeLength = 3;

    private const int NumberDigits = 4;

    /// <summary>
    /// A delegation's code for this purpose: its own short name if it set
    /// one, or the first letters of its full name if it did not.
    /// </summary>
    /// <remarks>
    /// A club is never required to set a short name — most never have
    /// needed to — so a credential cannot assume one exists. Deriving one
    /// here, consistently, is cheaper than asking every club to fill in a
    /// field it never otherwise needed.
    /// </remarks>
    public static string ClubCode(string? shortName, string fullName)
    {
        var source = string.IsNullOrWhiteSpace(shortName) ? fullName : shortName;

        var letters = new string([.. source
            .Where(char.IsLetterOrDigit)
            .Take(CodeLength)])
            .ToUpper(CultureInfo.InvariantCulture);

        // A name with fewer than three letters — "FC", say — still has to
        // fill the box; padding with the alphabet's own placeholder is more
        // honest than a trailing blank nobody would know how to read aloud.
        return letters.PadRight(CodeLength, 'X');
    }

    /// <summary>The identifier itself: a club code, a hyphen, a zero-padded number.</summary>
    public static string Format(string clubCode, int number) =>
        $"{clubCode}-{number.ToString($"D{NumberDigits}", CultureInfo.InvariantCulture)}";
}
