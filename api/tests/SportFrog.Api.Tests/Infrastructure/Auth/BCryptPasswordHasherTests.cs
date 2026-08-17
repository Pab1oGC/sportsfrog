using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Auth;

namespace SportFrog.Api.Tests.Infrastructure.Auth;

/// <summary>
/// RF-02 — a user's password is stored derived, never in plain text.
/// </summary>
public sealed class BCryptPasswordHasherTests
{
    // The lowest cost BCrypt accepts. Production uses DefaultWorkFactor; these
    // tests only assert the derivation, and a real cost would make them slow.
    private const int TestWorkFactor = 4;

    private const string PlainTextPassword = "Contraseña-Segura-2026";

    private readonly BCryptPasswordHasher _hasher = new(TestWorkFactor);

    [Fact]
    public void Hash_DoesNotReturnThePlainTextPassword()
    {
        var hash = _hasher.Hash(PlainTextPassword);

        hash.Should().NotBe(PlainTextPassword);
        hash.Should().NotContain(PlainTextPassword);
    }

    [Fact]
    public void Hash_ProducesABCryptHash()
    {
        var hash = _hasher.Hash(PlainTextPassword);

        // $<algorithm>$<cost>$<22 chars of salt><31 chars of digest>
        hash.Should().MatchRegex(@"^\$2[abxy]\$\d{2}\$[./A-Za-z0-9]{53}$");
        hash.Should().HaveLength(60);
    }

    [Fact]
    public void Hash_EmbedsTheConfiguredWorkFactor()
    {
        var hash = _hasher.Hash(PlainTextPassword);

        // Padded to two digits: the cost travels with the hash, so raising it
        // later doesn't invalidate what's already stored.
        hash.Should().MatchRegex($@"^\$2[abxy]\${TestWorkFactor:D2}\$");
    }

    [Fact]
    public void Hash_ProducesADifferentHashEachTime()
    {
        var first = _hasher.Hash(PlainTextPassword);
        var second = _hasher.Hash(PlainTextPassword);

        // Each hash carries its own salt: two users with the same password
        // must not be recognizable as such in the database.
        first.Should().NotBe(second);
    }

    [Fact]
    public void Hash_ProducesAHashThatVerifiesAgainstItsOwnPassword()
    {
        var hash = _hasher.Hash(PlainTextPassword);

        _hasher.Verify(PlainTextPassword, hash).Should().BeTrue();
    }

    [Fact]
    public void Hash_ProducesAHashThatRejectsAnyOtherPassword()
    {
        var hash = _hasher.Hash(PlainTextPassword);

        _hasher.Verify("Contraseña-Segura-2027", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_IsCaseSensitive()
    {
        var hash = _hasher.Hash(PlainTextPassword);

        _hasher.Verify(PlainTextPassword.ToLowerInvariant(), hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("áéíóú ñ ü")]
    [InlineData("🔐🏐")]
    [InlineData("   spaces   at   both   ends   ")]
    public void Hash_HandlesNonAsciiAndPaddedPasswords(string password)
    {
        var hash = _hasher.Hash(password);

        _hasher.Verify(password, hash).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_RejectsAnAbsentPassword(string? password)
    {
        var hashing = () => _hasher.Hash(password!);

        hashing.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(32)]
    public void Constructor_RejectsAWorkFactorOutsideTheAcceptedRange(int workFactor)
    {
        var construction = () => new BCryptPasswordHasher(workFactor);

        construction.Should().Throw<ArgumentOutOfRangeException>();
    }
}
