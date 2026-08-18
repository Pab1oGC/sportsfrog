namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Derives and verifies passwords with BCrypt, a key derivation function
/// with an adjustable cost factor — never a general-purpose digest, never
/// plain text (RF-02, RNF-10).
///
/// The cost travels inside the hash, so raising <see cref="DefaultWorkFactor"/>
/// later does not invalidate what is already stored: old hashes keep
/// verifying at the cost they were produced with.
/// </summary>
public sealed class BCryptPasswordHasher
{
    /// <summary>Lowest cost BCrypt accepts. Only sensible in tests.</summary>
    private const int MinimumWorkFactor = 4;

    /// <summary>Highest cost the algorithm encodes.</summary>
    private const int MaximumWorkFactor = 31;

    /// <summary>Cost used in production, absent an explicit configuration.</summary>
    public const int DefaultWorkFactor = 12;

    private readonly int _workFactor;

    public BCryptPasswordHasher(int workFactor = DefaultWorkFactor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workFactor, MinimumWorkFactor);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(workFactor, MaximumWorkFactor);

        _workFactor = workFactor;
    }

    /// <summary>
    /// Derives a password. Each call draws its own salt, so two people with
    /// the same password are not recognizable as such in the database.
    /// </summary>
    /// <exception cref="ArgumentException">The password is absent or blank.</exception>
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // Enhanced pre-hashes the input before BCrypt. Plain BCrypt only
        // reads the first 72 bytes, which would silently make two long
        // passwords sharing that prefix interchangeable.
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, _workFactor);
    }

    /// <summary>
    /// Compares an entered password against a stored hash. The cost is read
    /// from the hash itself, not from this instance's configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Either value is absent or blank.</exception>
    public bool Verify(string password, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
        }
        catch (Exception exception) when (
            exception is BCrypt.Net.SaltParseException
                or FormatException
                or ArgumentException)
        {
            // A corrupted column must fail the login attempt, not take the
            // endpoint down with it.
            return false;
        }
    }
}
