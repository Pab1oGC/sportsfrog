using AwesomeAssertions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Domain.Tests.Scheduling;

/// <summary>
/// Draws an all-play-all calendar with the circle method. Expected values
/// below come from the mathematics of round-robin scheduling itself — every
/// pair meets once, an odd field needs a full extra round so everyone's bye
/// lands somewhere, two legs double the fixtures and mirror the venue — not
/// from reading RoundRobin's own source. Where the classic algorithm leaves
/// a genuine implementation choice open (which team is held fixed, which
/// direction the circle turns), the test checks the invariant that has to
/// hold regardless of that choice, not one arbitrary schedule.
/// </summary>
public sealed class RoundRobinTests
{
    private static Guid[] Teams(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];

    private static IEnumerable<Guid> Players(DrawnMatch match)
    {
        yield return match.HomeTeamId;
        yield return match.AwayTeamId;
    }

    // ---- Round and match counts ------------------------------------------

    [Fact]
    public void Draw_EvenCount_OneLeg_PlaysNMinusOneRounds()
    {
        // Four teams: every team faces three opponents, one per round —
        // three rounds, no team ever idle.
        var drawn = RoundRobin.Draw(Teams(4), legs: 1);

        drawn.Select(match => match.Round).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void Draw_EvenCount_OneLeg_PlaysEveryPairExactlyOnce()
    {
        var teams = Teams(6);
        var drawn = RoundRobin.Draw(teams, legs: 1);

        // C(6,2) = 15 distinct pairings, each exactly once.
        drawn.Should().HaveCount(15);

        var pairs = drawn
            .Select(match => new[] { match.HomeTeamId, match.AwayTeamId }.OrderBy(id => id).ToArray())
            .Select(pair => (pair[0], pair[1]))
            .ToList();

        pairs.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Draw_OddCount_NeedsAFullExtraRound()
    {
        // Five teams cannot pair off; whoever sits out still needs a round
        // of their own, so an odd field plays N rounds, not N-1.
        var drawn = RoundRobin.Draw(Teams(5), legs: 1);

        drawn.Select(match => match.Round).Distinct().Should().HaveCount(5);
    }

    [Fact]
    public void Draw_OddCount_StillPlaysEveryPairExactlyOnceAndNothingMore()
    {
        var drawn = RoundRobin.Draw(Teams(5), legs: 1);

        // C(5,2) = 10 — the phantom never produces a real fixture.
        drawn.Should().HaveCount(10);
    }

    // ---- One match per team per round, and nobody skipped -----------------

    [Fact]
    public void Draw_NoTeamPlaysTwiceInTheSameRound()
    {
        var drawn = RoundRobin.Draw(Teams(6), legs: 1);

        foreach (var round in drawn.GroupBy(match => match.Round))
        {
            var appearances = round.SelectMany(Players).ToList();
            appearances.Should().OnlyHaveUniqueItems(
                because: $"round {round.Key} cannot ask one team to play two matches at once");
        }
    }

    [Fact]
    public void Draw_EvenCount_EveryTeamPlaysEveryRound()
    {
        var teams = Teams(6);
        var drawn = RoundRobin.Draw(teams, legs: 1);
        var rounds = drawn.Select(match => match.Round).Distinct().Count();

        foreach (var team in teams)
        {
            var roundsPlayed = drawn.Count(match => match.HomeTeamId == team || match.AwayTeamId == team);
            roundsPlayed.Should().Be(rounds, because: "with an even field nobody ever has a bye");
        }
    }

    [Fact]
    public void Draw_OddCount_EveryTeamSitsOutExactlyOneRound()
    {
        // A forced consequence of the numbers, not a design choice: five
        // teams, five rounds, and each team plays the other four exactly
        // once — which only adds up if each one is missing from exactly one
        // round.
        var teams = Teams(5);
        var drawn = RoundRobin.Draw(teams, legs: 1);
        var rounds = drawn.Select(match => match.Round).Distinct().ToList();

        foreach (var team in teams)
        {
            var roundsMissing = rounds.Count(round =>
                !drawn.Any(match => match.Round == round && (match.HomeTeamId == team || match.AwayTeamId == team)));

            roundsMissing.Should().Be(1, because: $"team {team} must sit exactly one round, not zero or two");
        }
    }

    // ---- Fairness: nobody parked on one side of the ball for the whole draw ----

    [Fact]
    public void Draw_NoTeamIsAlwaysHomeOrAlwaysAway()
    {
        // The whole reason a round-robin generator alternates sides: with a
        // large enough field, a team appearing in several matches has to
        // show up on both sides of at least one of them, or the calendar
        // reads as rigged toward whichever team the algorithm favours.
        var teams = Teams(6);
        var drawn = RoundRobin.Draw(teams, legs: 1);

        foreach (var team in teams)
        {
            var home = drawn.Count(match => match.HomeTeamId == team);
            var away = drawn.Count(match => match.AwayTeamId == team);

            home.Should().BeGreaterThan(0, because: $"team {team} played 5 matches; it cannot be away in all of them");
            away.Should().BeGreaterThan(0, because: $"team {team} played 5 matches; it cannot be home in all of them");
        }
    }

    // ---- Two legs: the same season played back, sides reversed ------------

    [Fact]
    public void Draw_TwoLegs_ExactlyDoublesTheFixtures()
    {
        var oneLeg = RoundRobin.Draw(Teams(4), legs: 1);
        var twoLegs = RoundRobin.Draw(Teams(4), legs: 2);

        // Not the same teams between the two calls, so compare counts only.
        twoLegs.Should().HaveCount(oneLeg.Count * 2);
    }

    [Fact]
    public void Draw_TwoLegs_SecondLegMirrorsTheFirstWithSidesSwapped()
    {
        var teams = Teams(4);
        var drawn = RoundRobin.Draw(teams, legs: 2);

        var firstLegRounds = drawn.Select(match => match.Round).Distinct().OrderBy(r => r).Take(3).ToList();
        var firstLeg = drawn.Where(match => firstLegRounds.Contains(match.Round)).ToList();
        var secondLeg = drawn.Except(firstLeg).ToList();

        secondLeg.Should().HaveCount(firstLeg.Count);

        // Home and away trade places for the return fixture, and every
        // return fixture happens strictly after every first-leg fixture —
        // "vuelta" means the second half of the season, not fixtures
        // scattered through the first.
        foreach (var match in firstLeg)
        {
            secondLeg.Should().ContainSingle(reverse =>
                reverse.HomeTeamId == match.AwayTeamId && reverse.AwayTeamId == match.HomeTeamId);
        }

        secondLeg.Min(match => match.Round).Should().BeGreaterThan(firstLeg.Max(match => match.Round));
    }

    // ---- Degenerate input ---------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Draw_FewerThanTwoTeams_ProducesNoFixtures(int count)
    {
        RoundRobin.Draw(Teams(count), legs: 1).Should().BeEmpty();
    }
}
