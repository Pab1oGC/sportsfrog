using AwesomeAssertions;
using SportFrog.Api.Features.Draw;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Tests.Features.Draw;

/// <summary>
/// The rows a bracket drawn in full turns into — what a group stage's
/// promotion and a pure knockout both create: every round down to the final,
/// with the later ones pointing at the matches whose winners fill them.
/// </summary>
public sealed class PlannedBracketTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Competition = Guid.NewGuid();
    private static readonly Guid Category = Guid.NewGuid();

    private static List<Guid> Teams(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())];

    private static List<Match> Rows(IReadOnlyList<Guid> teams) =>
        PlannedBracket.ToMatches(Bracket.FullDraw(teams), Org, Competition, Category);

    [Fact]
    public void ToMatches_EightQualifiers_CreatesEveryMatchDownToTheFinal()
    {
        var rows = Rows(Teams(8));

        rows.Should().HaveCount(7);
        rows.Select(row => row.RoundNumber).Should().Equal(1, 1, 1, 1, 2, 2, 3);
        rows.Select(row => row.Phase).Should().Equal(
            "cuartos", "cuartos", "cuartos", "cuartos", "semifinal", "semifinal", "final");
        rows.Select(row => row.Id).Distinct().Should().HaveCount(7);
        rows.Should().OnlyContain(row =>
            row.OrgId == Org && row.CompetitionId == Competition && row.CategoryId == Category
            && row.Status == MatchState.Scheduled);
    }

    [Fact]
    public void ToMatches_OnlyTheFirstRoundHasTeams_EveryLaterSideWaitsOnAMatch()
    {
        var teams = Teams(8);
        var rows = Rows(teams);

        rows.Take(4).Should().OnlyContain(row =>
            row.HomeTeamId != null && row.AwayTeamId != null
            && row.HomeSourceMatchId == null && row.AwaySourceMatchId == null);

        rows.Skip(4).Should().OnlyContain(row =>
            row.HomeTeamId == null && row.AwayTeamId == null
            && row.HomeSourceMatchId != null && row.AwaySourceMatchId != null);

        rows.Take(4).SelectMany(row => new[] { row.HomeTeamId!.Value, row.AwayTeamId!.Value })
            .Should().BeEquivalentTo(teams);
    }

    [Fact]
    public void ToMatches_LaterRounds_PointAtTheRowsThatFeedThem()
    {
        var rows = Rows(Teams(8));

        // Semi-finals take the winners of consecutive quarter-finals; the
        // final takes the two semi-finals. Ids resolve to the real rows, not
        // to positions in the plan.
        rows[4].HomeSourceMatchId.Should().Be(rows[0].Id);
        rows[4].AwaySourceMatchId.Should().Be(rows[1].Id);
        rows[5].HomeSourceMatchId.Should().Be(rows[2].Id);
        rows[5].AwaySourceMatchId.Should().Be(rows[3].Id);
        rows[6].HomeSourceMatchId.Should().Be(rows[4].Id);
        rows[6].AwaySourceMatchId.Should().Be(rows[5].Id);
    }

    [Fact]
    public void ToMatches_SixQualifiers_ByesGoStraightIntoTheSecondRoundAsKnownTeams()
    {
        // Two groups' worth of qualifiers that are not a power of two: the
        // top two seeds sit out round one, and enter round two by name.
        var teams = Teams(6);
        var rows = Rows(teams);

        rows.Should().HaveCount(5);
        rows.Select(row => row.RoundNumber).Should().Equal(1, 1, 2, 2, 3);

        // Round one plays the four teams after the byes.
        rows.Take(2).SelectMany(row => new[] { row.HomeTeamId!.Value, row.AwayTeamId!.Value })
            .Should().BeEquivalentTo(teams.Skip(2));

        // The first semi-final is the two byes against each other: both known,
        // nothing to wait on.
        rows[2].HomeTeamId.Should().Be(teams[0]);
        rows[2].AwayTeamId.Should().Be(teams[1]);
        rows[2].HomeSourceMatchId.Should().BeNull();
        rows[2].AwaySourceMatchId.Should().BeNull();

        // The second one waits on both round-one matches.
        rows[3].HomeTeamId.Should().BeNull();
        rows[3].AwayTeamId.Should().BeNull();
        rows[3].HomeSourceMatchId.Should().Be(rows[0].Id);
        rows[3].AwaySourceMatchId.Should().Be(rows[1].Id);

        rows[4].Phase.Should().Be("final");
    }

    [Fact]
    public void ToMatches_TwoQualifiers_IsJustTheFinal()
    {
        var rows = Rows(Teams(2));

        rows.Should().ContainSingle();
        rows[0].Phase.Should().Be("final");
        rows[0].HomeTeamId.Should().NotBeNull();
    }

    [Fact]
    public void ToMatches_NobodyToPlay_CreatesNothing()
    {
        Rows(Teams(1)).Should().BeEmpty();
    }
}
