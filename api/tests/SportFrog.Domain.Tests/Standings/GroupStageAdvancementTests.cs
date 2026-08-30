using AwesomeAssertions;
using SportFrog.Domain.Standings;

namespace SportFrog.Domain.Tests.Standings;

/// <summary>
/// A category that played groups first needs its knockout drawn from the
/// tables those groups produced — who qualifies, and in what order, so
/// round one never repeats a match the groups already settled unless the
/// numbers themselves leave no other way to pair everyone.
/// </summary>
public sealed class GroupStageAdvancementTests
{
    private static StandingsRow Row(string group, string name, int points, int scoreDifference = 0, int scoreFor = 0) =>
        new()
        {
            TeamId = Guid.NewGuid(),
            TeamName = name,
            GroupLabel = group,
            Points = points,
            ScoreFor = scoreFor,
            ScoreAgainst = scoreFor - scoreDifference,
        };

    private static StandingsGroup Group(string label, params StandingsRow[] ranked) =>
        new(label, ranked);

    [Fact]
    public void Build_FourGroupsTwoEach_QualifiesEightWithNoByesAndNoRematch()
    {
        var groups = new[]
        {
            Group("A", Row("A", "A1", 9), Row("A", "A2", 6)),
            Group("B", Row("B", "B1", 9), Row("B", "B2", 6)),
            Group("C", Row("C", "C1", 9), Row("C", "C2", 6)),
            Group("D", Row("D", "D1", 9), Row("D", "D2", 6)),
        };

        var groupOf = groups
            .SelectMany(group => group.Rows)
            .ToDictionary(row => row.TeamId, row => row.GroupLabel);

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 2, bestThirdPlaced: 0);

        problem.Should().BeNull();
        plan.Should().NotBeNull();
        plan!.Direct.Should().Be(8);
        plan.Wildcards.Should().Be(0);
        plan.Seeded.Should().HaveCount(8);
        plan.RepeatedMatchups.Should().Be(0);

        // Eight qualifiers is already a power of two: nobody gets a bye, and
        // round one pairs (0,1), (2,3), (4,5), (6,7).
        for (var i = 0; i < plan.Seeded.Count; i += 2)
        {
            groupOf[plan.Seeded[i]].Should().NotBe(groupOf[plan.Seeded[i + 1]],
                "round one should never repeat a group-stage matchup when there is any other way to pair everyone");
        }
    }

    [Fact]
    public void Build_RefusesAGroupThatCannotFillItsDirectQuota()
    {
        var groups = new[]
        {
            Group("A", Row("A", "A1", 9), Row("A", "A2", 6)),
            Group("B", Row("B", "B1", 9)), // only one team — can't fill two direct slots
        };

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 2, bestThirdPlaced: 0);

        plan.Should().BeNull();
        problem.Should().Contain("B");
    }

    [Fact]
    public void Build_RefusesMoreBestThirdPlacedThanGroupsCanSupply()
    {
        // Every group here has exactly two teams, so with two qualifying
        // direct there is no third-place team left in any of them to pull a
        // wildcard from.
        var groups = new[]
        {
            Group("A", Row("A", "A1", 9), Row("A", "A2", 6)),
            Group("B", Row("B", "B1", 9), Row("B", "B2", 6)),
            Group("C", Row("C", "C1", 9), Row("C", "C2", 6)),
        };

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 2, bestThirdPlaced: 1);

        plan.Should().BeNull();
        problem.Should().Contain("0");
    }

    [Fact]
    public void Build_RanksWildcardsAcrossGroupsByPointsThenGoalDifferenceThenGoalsFor()
    {
        var strongestThird = Row("A", "A3-best", points: 6, scoreDifference: 5, scoreFor: 10);
        var middleThird = Row("B", "B3-mid", points: 6, scoreDifference: 2, scoreFor: 8);
        var weakestThird = Row("C", "C3-worst", points: 4);

        var groups = new[]
        {
            Group("A", Row("A", "A1", 9), Row("A", "A2", 8), strongestThird),
            Group("B", Row("B", "B1", 9), Row("B", "B2", 8), middleThird),
            Group("C", Row("C", "C1", 9), Row("C", "C2", 8), weakestThird),
        };

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 2, bestThirdPlaced: 2);

        problem.Should().BeNull();
        plan.Should().NotBeNull();
        plan!.Wildcards.Should().Be(2);
        plan.Seeded.Should().Contain(strongestThird.TeamId);
        plan.Seeded.Should().Contain(middleThird.TeamId);
        plan.Seeded.Should().NotContain(weakestThird.TeamId);
    }

    [Fact]
    public void Build_OddTotalStillProducesAValidSeedingWithByes()
    {
        // Three groups, one direct qualifier each: an odd total that has to
        // round up to a bracket of four, handing one team a bye.
        var groups = new[]
        {
            Group("A", Row("A", "A1", 9)),
            Group("B", Row("B", "B1", 9)),
            Group("C", Row("C", "C1", 9)),
        };

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 1, bestThirdPlaced: 0);

        problem.Should().BeNull();
        plan!.Seeded.Should().HaveCount(3);
        plan.RepeatedMatchups.Should().Be(0);
    }

    [Fact]
    public void Build_ASingleGroupCannotAvoidARematch_AndSaysSoRatherThanFailing()
    {
        // Nothing to cross-pair against: both direct qualifiers come from
        // the only group there is, and there is no way to seat them apart.
        var groups = new[] { Group("A", Row("A", "A1", 9), Row("A", "A2", 6)) };

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 2, bestThirdPlaced: 0);

        problem.Should().BeNull();
        plan.Should().NotBeNull();
        plan!.Seeded.Should().HaveCount(2);
        plan.RepeatedMatchups.Should().Be(1);
    }

    [Fact]
    public void Build_RefusesWhenFewerThanTwoWouldQualify()
    {
        var groups = new[] { Group("A", Row("A", "A1", 9)) };

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup: 1, bestThirdPlaced: 0);

        plan.Should().BeNull();
        problem.Should().NotBeNull();
    }

    [Theory]
    [InlineData(3, 3, 2, 1)] // odd group count, best-thirds wildcards on top
    [InlineData(5, 4, 1, 3)] // winners-only direct, several best-seconds as wildcards
    [InlineData(6, 3, 2, 0)] // even group count, no wildcards
    public void Build_NeverRepeatsAGroupStageMatchup_WhenTheNumbersAllowAvoidingIt(
        int groupCount, int teamsPerGroup, int qualifiersPerGroup, int bestThirdPlaced)
    {
        var groups = new List<StandingsGroup>();

        for (var g = 0; g < groupCount; g++)
        {
            var label = ((char)('A' + g)).ToString();
            var rows = new StandingsRow[teamsPerGroup];

            for (var r = 0; r < teamsPerGroup; r++)
            {
                // Ranked strongest-first already, as a real table would hand
                // it over — descending points is all Reseed's collision
                // check ever looks past.
                rows[r] = Row(label, $"{label}{r}", points: (teamsPerGroup - r) * 3);
            }

            groups.Add(Group(label, rows));
        }

        var groupOf = groups
            .SelectMany(group => group.Rows)
            .ToDictionary(row => row.TeamId, row => row.GroupLabel);

        var (plan, problem) = GroupStageAdvancement.Build(groups, qualifiersPerGroup, bestThirdPlaced);

        problem.Should().BeNull();
        plan.Should().NotBeNull();

        // More than one group is playing, so a rematch is always avoidable —
        // the whole point of Reseed.
        plan!.RepeatedMatchups.Should().Be(0);

        var byeCount = NextPowerOfTwo(plan.Seeded.Count) - plan.Seeded.Count;

        for (var i = byeCount; i + 1 < plan.Seeded.Count; i += 2)
        {
            groupOf[plan.Seeded[i]].Should().NotBe(groupOf[plan.Seeded[i + 1]]);
        }
    }

    private static int NextPowerOfTwo(int count)
    {
        var size = 1;

        while (size < count)
        {
            size *= 2;
        }

        return size;
    }
}
