using System.Security.Cryptography;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Mints renewal tokens and derives the value stored for them.
///
/// The access token cannot be recalled once signed, so closing a session on
/// demand is this token's job: it lives in the database and can be revoked
/// there.
/// </summary>
public static class RefreshTokenFactory
{
    /// <summary>
    /// 256 bits of randomness. Enough that the stored digest cannot be
    /// attacked by guessing what produced it.
    /// </summary>
    private const int TokenBytes = 32;

    /// <summary>
    /// A new token and the value to store for it. The token is returned once,
    /// to be handed to the caller and never persisted.
    /// </summary>
    public static (string Token, string Hash) Create()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        return (token, Hash(token));
    }

    /// <summary>
    /// Derives the stored value for a token, so a presented one can be looked
    /// up.
    /// </summary>
    /// <remarks>
    /// A plain digest, not a password derivation function, and that is
    /// deliberate on both counts. The lookup is by equality against a unique
    /// column, which a per-token salt would make impossible; and the input is
    /// 256 random bits rather than something a person chose, so there is no
    /// dictionary to slow an attacker down with.
    ///
    /// What matters is what it buys: a database dump yields digests, and a
    /// digest cannot be presented as a token.
    /// </remarks>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
