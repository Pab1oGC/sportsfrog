using AwesomeAssertions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Domain.Tests.Scheduling;

/// <summary>
/// Kyorugi's repechage ladder for one finalist's half of the draw. Expected
/// values below come from the World Taekwondo rule this models — the
/// finalist's earliest-round opponent plays first, the semifinal loser enters
/// last because they alone have not already had to win once to stay in it,
/// and the last match is the one that actually decides the bronze — not from
/// reading Repechage's own source.
/// </summary>
public sealed class RepechageTests
{
    private static Guid[] Teams(int count) => [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];

    // ---- Degenerate halves -------------------------------------------------

    [Fact]
    public void BuildHalf_NoOpponentsBeaten_HasNoMatchAndNoBronze()
    {
        // The finalist reached the final without beating anyone at all — a
        // two-competitor category. There is nobody to award a bronze to on
        // this side.
        var half = Repechage.BuildHalf([]);

        half.Matches.Should().BeEmpty();
        half.AutomaticBronze.Should().BeNull();
    }

    [Fact]
    public void BuildHalf_OneOpponentBeaten_IsAnAutomaticBronzeWithNoMatch()
    {
        // Only the semifinal loser is eligible on this side — the same shape
        // as a lone bye advancing without playing.
        var teams = Teams(1);
        var half = Repechage.BuildHalf(teams);

        half.Matches.Should().BeEmpty();
        half.AutomaticBronze.Should().Be(teams[0]);
    }

    // ---- A real ladder ------------------------------------------------------

    [Fact]
    public void BuildHalf_TwoOpponentsBeaten_IsASingleMatchThatDecidesBronze()
    {
        var teams = Teams(2);
        var half = Repechage.BuildHalf(teams);

        half.AutomaticBronze.Should().BeNull();
        half.Matches.Should().ContainSingle();
        half.Matches[0].Phase.Should().Be(Repechage.BronzePhase);
        half.Matches[0].Home.TeamId.Should().Be(teams[0]);
        half.Matches[0].Away.TeamId.Should().Be(teams[1]);
    }

    [Fact]
    public void BuildHalf_ThreeOpponentsBeaten_TheEarliestTwoPlayFirst()
    {
        // Round one's loser against round two's loser — the semifinal loser
        // (last in the list) is not in this match at all.
        var teams = Teams(3);
        var half = Repechage.BuildHalf(teams);

        half.Matches.Should().HaveCount(2);
        half.Matches[0].Home.TeamId.Should().Be(teams[0]);
        half.Matches[0].Away.TeamId.Should().Be(teams[1]);
        half.Matches[0].Phase.Should().Be(Repechage.LadderPhase);
    }

    [Fact]
    public void BuildHalf_ThreeOpponentsBeaten_TheSemifinalLoserEntersOnlyTheLastMatch()
    {
        var teams = Teams(3);
        var half = Repechage.BuildHalf(teams);

        var last = half.Matches[^1];

        last.Phase.Should().Be(Repechage.BronzePhase);
        last.Away.TeamId.Should().Be(teams[2]);
        last.Home.TeamId.Should().BeNull("the other side is whoever wins the first match, not a known team yet");
        last.Home.SourceMatchIndex.Should().Be(0);
    }

    [Fact]
    public void BuildHalf_FiveOpponentsBeaten_ChainsEveryRungToTheOneBeforeIt()
    {
        var teams = Teams(5);
        var half = Repechage.BuildHalf(teams);

        half.Matches.Should().HaveCount(4);

        // First rung is two known entrants; every rung after is the previous
        // rung's still-unknown winner against the next opponent in line.
        half.Matches[0].Home.TeamId.Should().Be(teams[0]);
        half.Matches[0].Away.TeamId.Should().Be(teams[1]);

        for (var i = 1; i < half.Matches.Count; i++)
        {
            half.Matches[i].Home.SourceMatchIndex.Should().Be(i - 1);
            half.Matches[i].Away.TeamId.Should().Be(teams[i + 1]);
        }
    }

    [Fact]
    public void BuildHalf_OnlyTheLastRung_DecidesTheBronze()
    {
        var teams = Teams(5);
        var half = Repechage.BuildHalf(teams);

        half.Matches.SkipLast(1).Should().OnlyContain(match => match.Phase == Repechage.LadderPhase);
        half.Matches[^1].Phase.Should().Be(Repechage.BronzePhase);
    }

    [Fact]
    public void BuildHalf_EveryFeederReference_PointsToAnEarlierMatchInTheSameHalf()
    {
        var half = Repechage.BuildHalf(Teams(6));

        for (var i = 0; i < half.Matches.Count; i++)
        {
            if (half.Matches[i].Home.SourceMatchIndex is { } homeSource)
            {
                homeSource.Should().BeLessThan(i);
            }

            if (half.Matches[i].Away.SourceMatchIndex is { } awaySource)
            {
                awaySource.Should().BeLessThan(i);
            }
        }
    }

    [Fact]
    public void BuildHalf_EveryBeatenOpponent_AppearsInExactlyOneSlot()
    {
        var teams = Teams(6);
        var half = Repechage.BuildHalf(teams);

        var knownSlots = half.Matches
            .SelectMany(match => new[] { match.Home, match.Away })
            .Where(slot => slot.TeamId is not null)
            .Select(slot => slot.TeamId!.Value)
            .ToList();

        knownSlots.Should().BeEquivalentTo(teams, because: "nobody beaten on this half is left out or entered twice");
    }
}
