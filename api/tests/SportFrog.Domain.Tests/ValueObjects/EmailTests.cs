using AwesomeAssertions;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Domain.Tests.ValueObjects;

/// <summary>
/// RF-02 — a user is created with an email address; a malformed one must be
/// rejected before it ever reaches the database.
/// </summary>
public sealed class EmailTests
{
    [Fact]
    public void Parse_RejectsTheRF02Example_UsuarioAtCom()
    {
        // Has an "@", but the domain carries no dot, so there's nowhere to
        // route mail to. This is the exact example the requirement names.
        var parsing = () => Email.Parse("usuario@com");

        parsing.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("usuario")]                          // no "@" at all
    [InlineData("usuario@servidor")]                  // domain without a dot — same defect as the RF-02 example
    [InlineData("@dominio.com")]                      // no local part
    [InlineData("usuario@")]                          // no domain
    [InlineData("usuario@@dominio.com")]              // two "@"
    [InlineData("usuario dominio.com")]               // "@" replaced by whitespace
    [InlineData("usuario@dominio.com extra text")]    // trailing garbage after a valid-looking address
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_RejectsAMalformedAddress(string malformedEmail)
    {
        var parsing = () => Email.Parse(malformedEmail);

        parsing.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_RejectsAnAbsentValue()
    {
        var parsing = () => Email.Parse(null!);

        parsing.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("usuario@dominio.com")]
    [InlineData("nombre.apellido@sub.dominio.org")]
    [InlineData("usuario+etiqueta@dominio.co")]
    public void Parse_AcceptsAWellFormedAddress(string validEmail)
    {
        // Paired control: proves the rejections above are about format
        // specifically, not a blanket failure.
        var parsing = () => Email.Parse(validEmail);

        parsing.Should().NotThrow();
    }

    [Fact]
    public void Parse_PreservesTheOriginalValue()
    {
        var email = Email.Parse("usuario@dominio.com");

        email.Value.Should().Be("usuario@dominio.com");
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForTheRF02Example_WithoutThrowing()
    {
        var parsing = () => Email.TryParse("usuario@com", out _);

        parsing.Should().NotThrow();
        Email.TryParse("usuario@com", out var email).Should().BeFalse();
        email.Should().BeNull();
    }

    [Fact]
    public void TryParse_ReturnsTrue_ForAWellFormedAddress()
    {
        var succeeded = Email.TryParse("usuario@dominio.com", out var email);

        succeeded.Should().BeTrue();
        email.Should().NotBeNull();
        email!.Value.Should().Be("usuario@dominio.com");
    }
}
