using AwesomeAssertions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Domain.Tests.Scheduling;

/// <summary>
/// Pairs entrants for a single-elimination knockout, one round at a time.
/// Expected values below come from what a knockout bracket is — a field
/// rounded up to a power of two, with byes covering the shortfall — and from
/// the specific contract already established for this system (byes to the
/// front of the list, phase names by match count), not from reading
/// Bracket's own source.
/// </summary>
public sealed class BracketTests
{
    private static Guid[] Teams(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];

    // ---- A field that is already a power of two -------------------------

    [Fact]
    public void FirstRound_PowerOfTwoField_NeedsNoByes()
    {
        var (matches, byes) = Bracket.FirstRound(Teams(8));

        byes.Should().BeEmpty();
        matches.Should().HaveCount(4);
    }

    [Fact]
    public void FirstRound_PowerOfTwoField_PairsConsecutiveEntrants()
    {
        // Position 0 against 1, 2 against 3 — the order handed in is the
        // only ranking a bracket has, so the first two in it meet, not the
        // first against the last.
        var teams = Teams(8);
        var (matches, _) = Bracket.FirstRound(teams);

        matches.Should().BeEquivalentTo(new[]
        {
            new DrawnMatch(1, teams[0], teams[1]),
            new DrawnMatch(1, teams[2], teams[3]),
            new DrawnMatch(1, teams[4], teams[5]),
            new DrawnMatch(1, teams[6], teams[7]),
        });
    }

    [Fact]
    public void FirstRound_TwoTeams_IsJustTheOneMatch()
    {
        var teams = Teams(2);
        var (matches, byes) = Bracket.FirstRound(teams);

        byes.Should().BeEmpty();
        matches.Should().ContainSingle().Which.Should().Be(new DrawnMatch(1, teams[0], teams[1]));
    }

    // ---- A field that is not a power of two -----------------------------

    [Fact]
    public void FirstRound_FiveEntrants_RoundsUpToEightWithThreeByes()
    {
        // The next power of two above five is eight; the three teams short
        // of a full bracket advance without playing.
        var (matches, byes) = Bracket.FirstRound(Teams(5));

        byes.Should().HaveCount(3);
        matches.Should().ContainSingle();
    }

    [Fact]
    public void FirstRound_ElevenEntrants_RoundsUpToSixteenWithFiveByes()
    {
        var (matches, byes) = Bracket.FirstRound(Teams(11));

        byes.Should().HaveCount(5);
        matches.Should().HaveCount(3);
    }

    [Fact]
    public void FirstRound_ByesAreTheFrontOfTheListInOrder()
    {
        // Byes go to whoever is first, in the order they arrived — an
        // organization that seeds its own draw hands teams in seeded, and
        // the strongest sides skipping round one is what seeding is for.
        var teams = Teams(5);
        var (_, byes) = Bracket.FirstRound(teams);

        byes.Should().Equal(teams[0], teams[1], teams[2]);
    }

    [Fact]
    public void FirstRound_PlayingEntrantsAreWhatIsLeftAfterByes_PairedConsecutively()
    {
        var teams = Teams(5);
        var (matches, byes) = Bracket.FirstRound(teams);

        // Whoever did not get a bye plays, in the order they were left in.
        var stillPlaying = teams.Except(byes).ToList();
        matches.Should().ContainSingle().Which.Should().Be(
            new DrawnMatch(1, stillPlaying[0], stillPlaying[1]));
    }

    [Fact]
    public void FirstRound_EveryEntrant_IsInExactlyOneMatchOrTheByeListNeverBoth()
    {
        var teams = Teams(11);
        var (matches, byes) = Bracket.FirstRound(teams);

        var playing = matches.SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId }).ToList();

        (playing.Count + byes.Count).Should().Be(teams.Length);
        playing.Intersect(byes).Should().BeEmpty();
        playing.Concat(byes).Distinct().Should().HaveCount(teams.Length, because: "nobody entered twice and nobody was dropped");
    }

    // ---- Degenerate input ---------------------------------------------

    [Fact]
    public void FirstRound_NoEntrants_ProducesNoMatchesAndNoByes()
    {
        var (matches, byes) = Bracket.FirstRound([]);

        matches.Should().BeEmpty();
        byes.Should().BeEmpty();
    }

    [Fact]
    public void FirstRound_OneEntrant_HasNobodyToPlayAndIsTheOnlyBye()
    {
        // A single entrant cannot be paired against anyone. There is
        // nothing here that plays, so the lone name can only be reported as
        // still waiting.
        var teams = Teams(1);
        var (matches, byes) = Bracket.FirstRound(teams);

        matches.Should().BeEmpty();
        byes.Should().Equal(teams[0]);
    }

    // ---- Which round the matches are recorded under ----------------------

    [Fact]
    public void FirstRound_DefaultsToRoundOne()
    {
        var (matches, _) = Bracket.FirstRound(Teams(2));

        matches.Should().OnlyContain(match => match.Round == 1);
    }

    [Fact]
    public void FirstRound_AnExplicitRoundNumber_IsAppliedToEveryMatch()
    {
        // Used to draw a later stage of the same bracket — the winners of a
        // group phase entering straight into what is, numerically, round
        // three of the category.
        var (matches, _) = Bracket.FirstRound(Teams(4), round: 3);

        matches.Should().OnlyContain(match => match.Round == 3);
    }

    // ---- Phase names, by how many matches make up the round --------------

    [Theory]
    [InlineData(1, "final")]
    [InlineData(2, "semifinal")]
    [InlineData(4, "cuartos")]
    [InlineData(8, "octavos")]
    [InlineData(16, "dieciseisavos")]
    public void Phase_NamedRoundSizes_ReturnTheirSpanishName(int matches, string expected)
    {
        Bracket.Phase(matches, round: 1).Should().Be(expected);
    }

    [Theory]
    [InlineData(32, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 4)]
    public void Phase_UnnamedRoundSizes_FallBackToTheRoundNumber(int matches, int round)
    {
        // Past sixteenths nobody calls it by a name — a bracket that large
        // is talked about by its round number instead.
        Bracket.Phase(matches, round).Should().Be($"ronda {round}");
    }
}
