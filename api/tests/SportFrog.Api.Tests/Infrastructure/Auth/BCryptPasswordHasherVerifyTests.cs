using System.Text;
using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Auth;

namespace SportFrog.Api.Tests.Infrastructure.Auth;

/// <summary>
/// RF-02 — logging in compares the entered password against the stored hash,
/// never against another plain text value.
/// </summary>
public sealed class BCryptPasswordHasherVerifyTests
{
    // The lowest cost BCrypt accepts. Kept low so the suite stays fast; the
    // work factor itself is exercised separately below.
    private const int TestWorkFactor = 4;

    private const string PlainTextPassword = "Contraseña-Segura-2026";

    private readonly BCryptPasswordHasher _hasher = new(TestWorkFactor);

    [Fact]
    public void Verify_ReturnsTrue_WhenThePasswordMatchesTheStoredHash()
    {
        var storedHash = _hasher.Hash(PlainTextPassword);

        _hasher.Verify(PlainTextPassword, storedHash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenThePasswordDoesNotMatchTheStoredHash()
    {
        var storedHash = _hasher.Hash(PlainTextPassword);

        _hasher.Verify("Contraseña-Incorrecta", storedHash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenOnlyTheCaseDiffers()
    {
        var storedHash = _hasher.Hash(PlainTextPassword);

        _hasher.Verify(PlainTextPassword.ToUpperInvariant(), storedHash).Should().BeFalse();
    }

    [Theory]
    [InlineData(PlainTextPassword + " ")]
    [InlineData(" " + PlainTextPassword)]
    public void Verify_ReturnsFalse_WhenTheEnteredPasswordHasExtraWhitespace(string enteredPassword)
    {
        var storedHash = _hasher.Hash(PlainTextPassword);

        // Verify must not trim: a password with a trailing space is a
        // different password, not the same one loosely typed.
        _hasher.Verify(enteredPassword, storedHash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTheHashBelongsToAnotherPassword()
    {
        var someoneElsesHash = _hasher.Hash("Otra-Contraseña-Distinta");

        _hasher.Verify(PlainTextPassword, someoneElsesHash).Should().BeFalse();
    }

    [Fact]
    public void Verify_IsIndependentOfWhichSaltProducedTheHash()
    {
        // Same password, hashed twice: each call gets its own salt, and both
        // must still verify against the same password.
        var firstHash = _hasher.Hash(PlainTextPassword);
        var secondHash = _hasher.Hash(PlainTextPassword);

        firstHash.Should().NotBe(secondHash);
        _hasher.Verify(PlainTextPassword, firstHash).Should().BeTrue();
        _hasher.Verify(PlainTextPassword, secondHash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReadsTheCostFromTheHashRatherThanFromTheCurrentConfiguration()
    {
        // Two organizations, two different configured costs. A hash produced
        // years ago at a lower cost must still verify under today's setting:
        // raising DefaultWorkFactor must never invalidate stored hashes.
        var hasherAtLowerCost = new BCryptPasswordHasher(workFactor: 4);
        var hasherAtHigherCost = new BCryptPasswordHasher(workFactor: 6);

        var hashProducedAtLowerCost = hasherAtLowerCost.Hash(PlainTextPassword);

        hasherAtHigherCost.Verify(PlainTextPassword, hashProducedAtLowerCost).Should().BeTrue();
    }

    [Fact]
    public void Verify_IsDeterministicAcrossRepeatedCalls()
    {
        var storedHash = _hasher.Hash(PlainTextPassword);

        // Guards against a comparison that mutates state or behaves
        // differently on a second read of the same row.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            _hasher.Verify(PlainTextPassword, storedHash).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$04$tooShortToBeReal")]
    [InlineData("plain text accidentally saved as a hash")]
    public void Verify_ReturnsFalse_ForAMalformedStoredHash_InsteadOfThrowing(string malformedHash)
    {
        // A corrupted column must fail the login attempt, not crash the
        // endpoint: this is what keeps a bad row from becoming an outage.
        var verifying = () => _hasher.Verify(PlainTextPassword, malformedHash);

        verifying.Should().NotThrow();
        verifying().Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_RejectsAnAbsentEnteredPassword(string? enteredPassword)
    {
        var storedHash = _hasher.Hash(PlainTextPassword);

        var verifying = () => _hasher.Verify(enteredPassword!, storedHash);

        verifying.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_RejectsAnAbsentStoredHash(string? storedHash)
    {
        var verifying = () => _hasher.Verify(PlainTextPassword, storedHash!);

        verifying.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Verify_TreatsDifferentUnicodeNormalizationFormsAsDifferentPasswords()
    {
        // "á" as one composed code point (NFC) versus "a" + a combining
        // accent (NFD) render identically but are different byte sequences.
        // Different input devices produce different forms for the same
        // accented password used throughout this Spanish-language system, so
        // this documents that Verify compares bytes, not rendered text.
        const string composed = "Contraseña";
        var decomposed = composed.Normalize(NormalizationForm.FormD);

        composed.Should().NotBe(decomposed, "the two forms must differ at the byte level for this test to mean anything");

        var storedHash = _hasher.Hash(composed);

        _hasher.Verify(decomposed, storedHash).Should().BeFalse();
    }

    [Fact]
    public void Verify_HandlesAPasswordAtBCryptsSeventyTwoByteLimit()
    {
        // BCrypt only uses the first 72 bytes of the input. Two passwords
        // that share that 72-byte prefix but diverge afterward must still be
        // told apart, or the extra characters would be silently ignored.
        var prefix72Bytes = new string('a', 72);
        var storedHash = _hasher.Hash(prefix72Bytes + "-tail-that-must-matter");

        _hasher.Verify(prefix72Bytes + "-tail-that-must-matter", storedHash).Should().BeTrue();
        _hasher.Verify(prefix72Bytes + "-different-tail", storedHash).Should().BeFalse();
    }
}
