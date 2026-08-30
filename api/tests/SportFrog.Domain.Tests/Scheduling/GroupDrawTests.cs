using AwesomeAssertions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Domain.Tests.Scheduling;

/// <summary>
/// The half of a group stage draw that happens before there is a single
/// fixture: seating every team into a group, at random, and — where the
/// organizer asked for it — one per pot per group.
/// </summary>
public sealed class GroupDrawTests
{
    private static SeededTeam Team(short? pot = null) => new(Guid.NewGuid(), pot);

    [Fact]
    public void Draw_RefusesFewerThanTwoGroups()
    {
        var (assignments, problem) = GroupDraw.Draw([Team(), Team(), Team()], groupCount: 1);

        assignments.Should().BeNull();
        problem.Should().NotBeNull();
    }

    [Fact]
    public void Draw_RefusesMoreGroupsThanTeams()
    {
        var (assignments, problem) = GroupDraw.Draw([Team(), Team(), Team()], groupCount: 4);

        assignments.Should().BeNull();
        problem.Should().Contain("3");
    }

    [Fact]
    public void Draw_UnseededDraw_SeatsEveryTeamAndBalancesTheGroups()
    {
        var teams = Enumerable.Range(0, 12).Select(_ => Team()).ToList();

        var (assignments, problem) = GroupDraw.Draw(teams, groupCount: 4);

        problem.Should().BeNull();
        assignments.Should().HaveCount(12);
        assignments!.Select(a => a.TeamId).Should().BeEquivalentTo(teams.Select(t => t.TeamId));

        var sizes = assignments.GroupBy(a => a.GroupLabel).Select(g => g.Count());
        sizes.Should().AllBeEquivalentTo(3, "twelve teams in four groups should split evenly");
    }

    [Fact]
    public void Draw_SeededDraw_PlacesExactlyOneTeamPerPotInEveryGroup()
    {
        // Four pots of four — the textbook shape: each group should end up
        // with exactly one team from every pot, never two from the same one.
        var pots = new List<SeededTeam>();

        for (short pot = 1; pot <= 4; pot++)
        {
            for (var i = 0; i < 4; i++)
            {
                pots.Add(Team(pot));
            }
        }

        var potOf = pots.ToDictionary(t => t.TeamId, t => t.Pot);

        var (assignments, problem) = GroupDraw.Draw(pots, groupCount: 4);

        problem.Should().BeNull();
        assignments.Should().HaveCount(16);

        foreach (var group in assignments!.GroupBy(a => a.GroupLabel))
        {
            var potsInGroup = group.Select(a => potOf[a.TeamId]).ToList();
            potsInGroup.Should().OnlyHaveUniqueItems("a seeded draw keeps one pot from meeting itself in a group");
            potsInGroup.Should().BeEquivalentTo([(short)1, (short)2, (short)3, (short)4]);
        }
    }

    [Fact]
    public void Draw_MixOfSeededAndUnseededTeams_StillSeatsEveryone()
    {
        // Two full pots plus a handful of unseeded teams filling out the rest
        // — not the textbook shape, but nobody should go missing or double up.
        List<SeededTeam> teams =
        [
            Team(1), Team(1), Team(1),
            Team(2), Team(2), Team(2),
            Team(), Team(), Team(),
        ];

        var (assignments, problem) = GroupDraw.Draw(teams, groupCount: 3);

        problem.Should().BeNull();
        assignments.Should().HaveCount(9);
        assignments!.Select(a => a.TeamId).Should().BeEquivalentTo(teams.Select(t => t.TeamId));
        assignments.Select(a => a.GroupLabel).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void Draw_RefusesAPotWithMoreTeamsThanGroups()
    {
        // Six teams marked pot 1, but only four groups to spread a pot over —
        // the promise "one per group" cannot be kept, no matter how they are
        // dealt. This is exactly the shape a stray or leftover pot number
        // produces.
        var teams = Enumerable.Range(0, 6).Select(_ => Team(1))
            .Concat(Enumerable.Range(0, 2).Select(_ => Team()))
            .ToList();

        var (assignments, problem) = GroupDraw.Draw(teams, groupCount: 4);

        assignments.Should().BeNull();
        problem.Should().Contain("bombo 1").And.Contain("6").And.Contain("4");
    }

    [Fact]
    public void Draw_APotSmallerThanTheGroupCountIsFine()
    {
        // Two favourites marked pot 1, everyone else open — a pot smaller
        // than the group count is the ordinary partial-seeding shape, not a
        // problem.
        List<SeededTeam> teams = [Team(1), Team(1), Team(), Team(), Team(), Team()];

        var (assignments, problem) = GroupDraw.Draw(teams, groupCount: 3);

        problem.Should().BeNull();
        assignments.Should().HaveCount(6);
    }

    [Fact]
    public void Draw_NotRespectingPots_AllowsAnOversizedPotAndCanPutItsTeamsTogether()
    {
        // Six teams in pot 1, only two groups: with pots respected this would
        // be refused outright — the oversized-pot check above proves it.
        // Ignoring pots, it is nothing more than six teams and two groups,
        // always possible, and by the pigeonhole principle at least one
        // group ends up with more than one of them — a league phase that
        // deliberately lets its own favourites meet, the way the newer
        // Champions League format does.
        var teams = Enumerable.Range(0, 6).Select(_ => Team(1)).ToList();

        var (assignments, problem) = GroupDraw.Draw(teams, groupCount: 2, respectPots: false);

        problem.Should().BeNull();
        assignments.Should().HaveCount(6);
        assignments!.GroupBy(a => a.GroupLabel).Select(g => g.Count())
            .Should().Contain(count => count > 1);
    }

    [Fact]
    public void Draw_IsActuallyRandom_NotTheSameSeatingEveryTime()
    {
        var teams = Enumerable.Range(0, 8).Select(_ => Team()).ToList();

        var outcomes = Enumerable.Range(0, 20)
            .Select(_ => GroupDraw.Draw(teams, groupCount: 4).Assignments!
                .OrderBy(a => a.TeamId)
                .Select(a => a.GroupLabel)
                .ToList())
            .Select(labels => string.Join(",", labels))
            .Distinct()
            .Count();

        // Twenty draws of eight teams into four groups landing on the exact
        // same seating every single time would mean this is not shuffling at
        // all.
        outcomes.Should().BeGreaterThan(1);
    }
}
