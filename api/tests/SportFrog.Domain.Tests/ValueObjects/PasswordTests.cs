using AwesomeAssertions;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Domain.Tests.ValueObjects;

/// <summary>
/// RF-02 — a user's password must meet a minimum security policy before it
/// ever reaches the hasher: minimum length, at least one special character,
/// at least one uppercase letter, one lowercase letter and one digit.
/// </summary>
public sealed class PasswordTests
{
    [Fact]
    public void Parse_RejectsAPasswordShorterThanTheMinimumLength()
    {
        // One character short of the minimum, otherwise compliant with
        // every other rule: isolates the length rule alone.
        var tooShort = "Ab1!" + new string('c', Password.MinimumLength - 5);
        tooShort.Should().HaveLength(Password.MinimumLength - 1);

        var parsing = () => Password.Parse(tooShort);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_AcceptsAPasswordAtExactlyTheMinimumLength()
    {
        var atMinimum = "Ab1!" + new string('c', Password.MinimumLength - 4);
        atMinimum.Should().HaveLength(Password.MinimumLength);

        var parsing = () => Password.Parse(atMinimum);

        parsing.Should().NotThrow();
    }

    [Fact]
    public void Parse_RejectsAPasswordWithoutAnUppercaseLetter()
    {
        // Long enough, lowercase, digit and special character present:
        // isolates the uppercase rule alone.
        var noUppercase = "abcdef1!";

        var parsing = () => Password.Parse(noUppercase);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_RejectsAPasswordWithoutALowercaseLetter()
    {
        var noLowercase = "ABCDEF1!";

        var parsing = () => Password.Parse(noLowercase);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_RejectsAPasswordWithoutADigit()
    {
        var noDigit = "Abcdefg!";

        var parsing = () => Password.Parse(noDigit);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_RejectsAPasswordWithoutAnySpecialCharacter()
    {
        // Long enough, letters and a digit, nothing else: isolates the
        // special-character rule from the length rule.
        var noSpecialCharacter = "Abcdefgh1";

        var parsing = () => Password.Parse(noSpecialCharacter);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_RejectsAPasswordWhereTheOnlyNonAlphanumericCharacterIsWhitespace()
    {
        // A naive "not a letter or digit" check would wrongly accept a
        // space as satisfying the rule. Whitespace isn't a special
        // character, it's the absence of one.
        var onlySpaceAsNonAlphanumeric = "Abcdefg 1";

        var parsing = () => Password.Parse(onlySpaceAsNonAlphanumeric);

        parsing.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData('!')]
    [InlineData('@')]
    [InlineData('#')]
    [InlineData('$')]
    [InlineData('%')]
    [InlineData('&')]
    [InlineData('*')]
    [InlineData('-')]
    [InlineData('_')]
    [InlineData('.')]
    public void Parse_AcceptsDifferentKindsOfSpecialCharacters(char specialCharacter)
    {
        var password = $"Abcdef1{specialCharacter}";
        password.Should().HaveLength(Password.MinimumLength);

        var parsing = () => Password.Parse(password);

        parsing.Should().NotThrow();
    }

    [Fact]
    public void Parse_RejectsAPasswordThatFailsMultipleRulesAtOnce()
    {
        // Too short, no uppercase, no digit, no special character: several
        // violations at the same time must still just be rejected once.
        var parsing = () => Password.Parse("abc");

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_RejectsAnAbsentValue()
    {
        var parsing = () => Password.Parse(null!);

        parsing.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RejectsAnEmptyOrWhitespaceOnlyValue(string blankPassword)
    {
        var parsing = () => Password.Parse(blankPassword);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_AcceptsAPasswordWithAccentedCharactersAndASpecialCharacter()
    {
        // Interface is in Spanish (RNF-09): an accented password is a
        // realistic input, not an edge case to special-case away.
        var parsing = () => Password.Parse("Contraseña-1");

        parsing.Should().NotThrow();
    }

    [Fact]
    public void Parse_PreservesTheOriginalValue()
    {
        const string compliantPassword = "Abcdef1!";

        var password = Password.Parse(compliantPassword);

        password.Value.Should().Be(compliantPassword);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForAWeakPassword_WithoutThrowing()
    {
        var parsing = () => Password.TryParse("weak", out _);

        parsing.Should().NotThrow();
        Password.TryParse("weak", out var password).Should().BeFalse();
        password.Should().BeNull();
    }

    [Fact]
    public void TryParse_ReturnsTrue_ForACompliantPassword()
    {
        const string compliantPassword = "Abcdef1!";

        var succeeded = Password.TryParse(compliantPassword, out var password);

        succeeded.Should().BeTrue();
        password.Should().NotBeNull();
        password!.Value.Should().Be(compliantPassword);
    }
}
