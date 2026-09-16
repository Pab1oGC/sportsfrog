using AwesomeAssertions;
using SportFrog.Domain.Matches;

namespace SportFrog.Domain.Tests.Matches;

/// <summary>
/// The exact behaviour <c>AdvanceBracket</c> had inline before this was
/// pulled out — every case here mirrors one branch that code used to have,
/// so the refactor could be checked against it one by one.
/// </summary>
public sealed class MatchWinnerTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();

    [Fact]
    public void Resolve_AWalkover_WinsRegardlessOfAnyScoreEntered()
    {
        // An awarded side wins even if a score somehow disagrees with it —
        // the award is the outcome, not a summary of one.
        MatchWinner.Resolve(Home, Away, walkoverTeamId: Away, homeTotal: 5, awayTotal: 0, null, null)
            .Should().Be(Away);
    }

    [Fact]
    public void Resolve_NoResultYet_IsUndecided()
    {
        MatchWinner.Resolve(Home, Away, null, null, null, null, null).Should().BeNull();
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(0, 1)]
    public void Resolve_AnUnequalScore_DecidesItOnItsOwn(int homeTotal, int awayTotal)
    {
        var winner = MatchWinner.Resolve(Home, Away, null, homeTotal, awayTotal, null, null);
        winner.Should().Be(homeTotal > awayTotal ? Home : Away);
    }

    [Fact]
    public void Resolve_ALevelScoreWithAShootout_IsDecidedByTheShootout()
    {
        MatchWinner.Resolve(Home, Away, null, homeTotal: 2, awayTotal: 2, penaltyHomeScore: 3, penaltyAwayScore: 4)
            .Should().Be(Away);

        MatchWinner.Resolve(Home, Away, null, homeTotal: 1, awayTotal: 1, penaltyHomeScore: 5, penaltyAwayScore: 4)
            .Should().Be(Home);
    }

    [Fact]
    public void Resolve_ALevelScoreWithNoShootoutRecorded_IsUndecided()
    {
        // The exact case AdvanceBracket refuses to advance from: a tie that
        // was never actually broken.
        MatchWinner.Resolve(Home, Away, null, homeTotal: 1, awayTotal: 1, null, null).Should().BeNull();
    }
}
