using AwesomeAssertions;
using SportFrog.Api.Features.Draw;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Tests.Features.Draw;

/// <summary>
/// Each format's own way of drawing a category — extracted from a switch
/// inside DrawCalendar's handler, where none of this was reachable on its
/// own. Expected results below come from what each format's draw is
/// supposed to do (a league plays everyone, a bracket pairs consecutively,
/// a group stage runs a league inside each group), the same knowledge
/// RoundRobinTests and BracketTests already worked out independently for
/// the algorithms these classes call.
/// </summary>
public sealed class CalendarDrawTests
{
    private static DrawnTeam[] Teams(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => new DrawnTeam(Guid.NewGuid(), null))];

    // ---- League ------------------------------------------------------

    [Fact]
    public void League_DeclaresItsOwnFormat()
    {
        new LeagueCalendarDraw().Format.Should().Be(CompetitionFormat.League);
    }

    [Fact]
    public void League_Inspect_NeverObjects()
    {
        new LeagueCalendarDraw().Inspect(Teams(3), legs: 2).Should().BeNull();
    }

    [Fact]
    public void League_Draw_PlaysEveryTeamAgainstEveryOther()
    {
        var teams = Teams(4);

        var (matches, phase, byes) = new LeagueCalendarDraw().Draw(teams, legs: 1);

        // Four teams, single leg: six fixtures, one round-robin, no phase or byes.
        matches.Should().HaveCount(6);
        phase.Should().BeNull();
        byes.Should().Be(0);
    }

    // ---- Groups ------------------------------------------------------

    [Fact]
    public void Groups_DeclaresItsOwnFormat()
    {
        new GroupsCalendarDraw().Format.Should().Be(CompetitionFormat.Groups);
    }

    [Fact]
    public void Groups_Inspect_NoTeamAssignedAGroup_Objects()
    {
        var problem = new GroupsCalendarDraw().Inspect(Teams(4), legs: 1);

        problem.Should().NotBeNull();
        problem.Should().Contain("grupo");
    }

    [Fact]
    public void Groups_Inspect_AtLeastOneTeamAssignedAGroup_DoesNotObject()
    {
        DrawnTeam[] teams = [new(Guid.NewGuid(), "A"), new(Guid.NewGuid(), null)];

        new GroupsCalendarDraw().Inspect(teams, legs: 1).Should().BeNull();
    }

    [Fact]
    public void Groups_Draw_RunsALeagueInsideEachGroupIndependently()
    {
        // Two groups of three: three fixtures each, six in total — none of
        // them crossing group lines, since RoundRobin.Draw only ever sees
        // one group's own teams at a time.
        DrawnTeam[] teams =
        [
            new(Guid.NewGuid(), "A"), new(Guid.NewGuid(), "A"), new(Guid.NewGuid(), "A"),
            new(Guid.NewGuid(), "B"), new(Guid.NewGuid(), "B"), new(Guid.NewGuid(), "B"),
        ];
        var groupA = teams.Take(3).Select(team => team.Id).ToHashSet();
        var groupB = teams.Skip(3).Select(team => team.Id).ToHashSet();

        var (matches, phase, byes) = new GroupsCalendarDraw().Draw(teams, legs: 1);

        matches.Should().HaveCount(6);
        phase.Should().BeNull();
        byes.Should().Be(0);
        matches.Should().OnlyContain(match =>
            (groupA.Contains(match.HomeTeamId) && groupA.Contains(match.AwayTeamId))
            || (groupB.Contains(match.HomeTeamId) && groupB.Contains(match.AwayTeamId)));
    }

    // ---- Knockout ------------------------------------------------------

    [Fact]
    public void Knockout_DeclaresItsOwnFormat()
    {
        new KnockoutCalendarDraw().Format.Should().Be(CompetitionFormat.Knockout);
    }

    [Fact]
    public void Knockout_Inspect_TwoLegs_Objects()
    {
        // A knockout tie is decided on aggregate, a different object from
        // two independent results — refused outright rather than drawn as
        // two ordinary matches.
        var problem = new KnockoutCalendarDraw().Inspect(Teams(4), legs: 2);

        problem.Should().NotBeNull();
    }

    [Fact]
    public void Knockout_Inspect_OneLeg_DoesNotObject()
    {
        new KnockoutCalendarDraw().Inspect(Teams(4), legs: 1).Should().BeNull();
    }

    [Fact]
    public void Knockout_Draw_PairsConsecutivelyAndNamesThePhase()
    {
        var teams = Teams(8);

        var (matches, phase, byes) = new KnockoutCalendarDraw().Draw(teams, legs: 1);

        matches.Should().HaveCount(4);
        phase.Should().Be("cuartos");
        byes.Should().Be(0);
    }

    [Fact]
    public void Knockout_Draw_NotAPowerOfTwo_ProducesByes()
    {
        var teams = Teams(5);

        var (matches, _, byes) = new KnockoutCalendarDraw().Draw(teams, legs: 1);

        matches.Should().ContainSingle();
        byes.Should().Be(3);
    }

    [Fact]
    public void Knockout_DrawFull_EightTeams_DrawsEveryRoundDownToTheFinal()
    {
        var draw = new KnockoutCalendarDraw();

        var plan = draw.DrawFull(Teams(8));

        // Cuartos (4) + semifinal (2) + final (1) — round one alone stops
        // after four; this is what a knockout promoted from nothing else
        // gets that the other formats never need.
        plan.Should().HaveCount(7);
        plan.Last().Phase.Should().Be("final");
    }

    [Fact]
    public void Knockout_DrawFull_RoundOne_AgreesWithDraw()
    {
        // Draw() is implemented in terms of DrawFull() precisely so the two
        // can never disagree about what round one looks like — this is that
        // guarantee, checked from the outside.
        var draw = new KnockoutCalendarDraw();
        var teams = Teams(5);

        var (matches, phase, byes) = draw.Draw(teams, legs: 1);
        var firstRound = draw.DrawFull(teams).Where(match => match.Round == 1).ToList();

        firstRound.Should().HaveCount(matches.Count);
        firstRound[0].Phase.Should().Be(phase);
        (teams.Length - firstRound.Count * 2).Should().Be(byes);
    }

    // ---- Registry ------------------------------------------------------

    [Fact]
    public void Registry_ResolvesEachFormatToItsOwnDraw()
    {
        var registry = new CalendarDrawRegistry(
            [new LeagueCalendarDraw(), new GroupsCalendarDraw(), new KnockoutCalendarDraw()]);

        registry.For(CompetitionFormat.League).Should().BeOfType<LeagueCalendarDraw>();
        registry.For(CompetitionFormat.Groups).Should().BeOfType<GroupsCalendarDraw>();
        registry.For(CompetitionFormat.Knockout).Should().BeOfType<KnockoutCalendarDraw>();
    }

    [Fact]
    public void Registry_UnregisteredFormat_ThrowsRatherThanSilentlyPickingOne()
    {
        var registry = new CalendarDrawRegistry([new LeagueCalendarDraw()]);

        var attempt = () => registry.For(CompetitionFormat.Knockout);

        attempt.Should().Throw<InvalidOperationException>();
    }
}
