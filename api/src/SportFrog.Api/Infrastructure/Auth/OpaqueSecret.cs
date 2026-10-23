using System.Security.Cryptography;
using System.Text;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Mints a bearer secret and derives the value stored for it.
///
/// Two things in this system are "something you hold rather than something
/// you know": a renewal token (<see cref="RefreshTokenFactory"/>) and a
/// password reset token (<see cref="PasswordResetTokens"/>). Both need the
/// same two operations and the same reasoning behind them, so both get it
/// from here rather than each carrying its own copy of the crypto.
/// </summary>
internal static class OpaqueSecret
{
    /// <summary>
    /// 256 bits of randomness. Enough that the stored digest cannot be
    /// attacked by guessing what produced it.
    /// </summary>
    private const int SecretBytes = 32;

    /// <summary>
    /// A new secret and the value to store for it. The secret is returned
    /// once, to be handed to its holder and never persisted.
    /// </summary>
    public static (string Secret, string Hash) Create()
    {
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));

        return (secret, Hash(secret));
    }

    /// <summary>
    /// Derives the stored value for a secret, so a presented one can be
    /// looked up.
    /// </summary>
    /// <remarks>
    /// A plain digest, not a password derivation function, and that is
    /// deliberate on both counts. The lookup is by equality against a unique
    /// column, which a per-secret salt would make impossible; and the input
    /// is 256 random bits rather than something a person chose, so there is
    /// no dictionary to slow an attacker down with.
    ///
    /// What matters is what it buys: a database dump yields digests, and a
    /// digest cannot be presented as a secret.
    /// </remarks>
    public static string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    /// <summary>
    /// Base64 without the characters that would have to be escaped to travel
    /// in a URL — a reset link carries one of these as a query parameter.
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
