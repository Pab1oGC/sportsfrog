using AwesomeAssertions;
using SportFrog.Domain.Documents;

namespace SportFrog.Domain.Tests.Documents;

/// <summary>
/// The identifier printed in a credential's data block — a club code and a
/// sequential number, distinct from the random serial the QR carries.
/// </summary>
public sealed class VisibleCredentialIdTests
{
    [Fact]
    public void ClubCode_ShortNameIsSet_UsesItUppercased()
    {
        VisibleCredentialId.ClubCode("ind", "Club Atlético Independiente").Should().Be("IND");
    }

    [Fact]
    public void ClubCode_NoShortName_FallsBackToTheFullName()
    {
        VisibleCredentialId.ClubCode(null, "Barcelona Sporting Club").Should().Be("BAR");
    }

    [Fact]
    public void ClubCode_ShortNameIsBlank_FallsBackToTheFullName()
    {
        VisibleCredentialId.ClubCode("   ", "Barcelona Sporting Club").Should().Be("BAR");
    }

    [Fact]
    public void ClubCode_NameHasFewerThanThreeLetters_PadsWithX()
    {
        VisibleCredentialId.ClubCode(null, "FC").Should().Be("FCX");
    }

    [Fact]
    public void ClubCode_NameStartsWithPunctuationOrSpaces_SkipsToTheFirstLetters()
    {
        VisibleCredentialId.ClubCode(null, "  — Deportivo Quito").Should().Be("DEP");
    }

    [Fact]
    public void ClubCode_NameIsEntirelyPunctuation_PadsEntirelyWithX()
    {
        VisibleCredentialId.ClubCode(null, "—").Should().Be("XXX");
    }

    [Fact]
    public void Format_PadsTheNumberToFourDigits()
    {
        VisibleCredentialId.Format("IND", 42).Should().Be("IND-0042");
    }

    [Fact]
    public void Format_NumberAlreadyFourDigits_PrintsItWhole()
    {
        VisibleCredentialId.Format("IND", 1234).Should().Be("IND-1234");
    }

    [Fact]
    public void Format_NumberExceedsFourDigits_IsNotTruncated()
    {
        // Correctness over a round-looking box: a batch large enough to pass
        // 9999 must never produce a number somebody else was already given.
        VisibleCredentialId.Format("IND", 12345).Should().Be("IND-12345");
    }
}
