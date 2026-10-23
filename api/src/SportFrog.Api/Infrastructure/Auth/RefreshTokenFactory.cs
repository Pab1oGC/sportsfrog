namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Mints renewal tokens and derives the value stored for them.
///
/// The access token cannot be recalled once signed, so closing a session on
/// demand is this token's job: it lives in the database and can be revoked
/// there.
/// </summary>
/// <remarks>
/// The randomness and the digest come from <see cref="OpaqueSecret"/>, which
/// a password reset token is built from too — see there for why a plain
/// SHA-256 rather than a password derivation function. This type remains the
/// name the session code reaches for, so a reader of
/// <see cref="SessionIssuer"/> finds renewal tokens where they expect them.
/// </remarks>
public static class RefreshTokenFactory
{
    /// <summary>
    /// A new token and the value to store for it. The token is returned once,
    /// to be handed to the caller and never persisted.
    /// </summary>
    public static (string Token, string Hash) Create() => OpaqueSecret.Create();

    /// <summary>
    /// Derives the stored value for a token, so a presented one can be looked
    /// up.
    /// </summary>
    public static string Hash(string token) => OpaqueSecret.Hash(token);
}
