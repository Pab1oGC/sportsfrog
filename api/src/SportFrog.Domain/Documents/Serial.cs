using System.Security.Cryptography;

namespace SportFrog.Domain.Documents;

/// <summary>
/// The code printed on a document, and the one public verification resolves.
/// </summary>
/// <remarks>
/// Random rather than sequential, and that is the whole design of this file.
///
/// A counter is the obvious thing to print on a credential and it is the wrong
/// thing here. Verification is anonymous by requirement (RF-45): anybody who
/// scans a card gets an answer without signing in. With sequential serials,
/// anybody who scans <em>one</em> card can walk the whole league — the same
/// address with the number one higher — and read back a list of who plays
/// where. For a competition of children that is precisely the disclosure the
/// public view is built to prevent (RNF-16). Sixty bits of randomness makes
/// that walk pointless.
///
/// The alphabet is Crockford's base 32, which drops I, L, O and U. Somebody
/// reads this off a card and types it into a phone with a cracked screen; a
/// zero that could be an O is a support call, and the U is dropped because
/// removing it keeps accidental words out.
///
/// Grouped in fours because that is how people read and dictate a code, and
/// the hyphens are cosmetic: they are stored, so what is printed and what is
/// looked up are the same string.
/// </remarks>
public static class Serial
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int Characters = 12;

    private const int GroupSize = 4;

    /// <summary>A fresh code, in the form it is printed and stored.</summary>
    public static string Next()
    {
        var code = new char[Characters + ((Characters / GroupSize) - 1)];
        var written = 0;

        for (var index = 0; index < Characters; index++)
        {
            if (index > 0 && index % GroupSize == 0)
            {
                code[written++] = '-';
            }

            // One draw per character rather than one number split up: this is
            // not a place to be clever, and RandomNumberGenerator gives an
            // unbiased index for free.
            code[written++] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
