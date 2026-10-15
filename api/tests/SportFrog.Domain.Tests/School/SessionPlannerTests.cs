using AwesomeAssertions;
using SportFrog.Domain.School;

namespace SportFrog.Domain.Tests.School;

/// <summary>
/// Turns a weekly schedule into the dates it actually falls on. Everything
/// here is about which dates come out and in what order — what a session
/// means once it exists is <see cref="AttendanceRateTests"/>'s concern, not
/// this one.
/// </summary>
public sealed class SessionPlannerTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Tuesday = Monday.AddDays(1);
    private static readonly DateOnly Thursday = Monday.AddDays(3);

    private static WeeklySlot Slot(DayOfWeek day, int startHour, int endHour) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    [Fact]
    public void Plan_NoSlots_ReturnsNothingEvenOverAWideRange()
    {
        SessionPlanner.Plan([], Monday, Monday.AddYears(1)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_RangeEndsBeforeItStarts_ReturnsNothingRatherThanThrowing()
    {
        SessionPlanner.Plan([Slot(DayOfWeek.Tuesday, 18, 19)], Thursday, Monday).Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleDayRangeOnTheSlotsDay_ReturnsExactlyThatOneSession()
    {
        var sessions = SessionPlanner.Plan([Slot(DayOfWeek.Tuesday, 18, 19)], Tuesday, Tuesday);

        sessions.Should().ContainSingle();
        sessions[0].Date.Should().Be(Tuesday);
        sessions[0].StartsAt.Should().Be(new TimeOnly(18, 0));
        sessions[0].EndsAt.Should().Be(new TimeOnly(19, 0));
    }

    [Fact]
    public void Plan_SingleDayRangeOffTheSlotsDay_ReturnsNothing()
    {
        SessionPlanner.Plan([Slot(DayOfWeek.Tuesday, 18, 19)], Monday, Monday).Should().BeEmpty();
    }

    [Fact]
    public void Plan_OneSlotAcrossFourWeeks_ReturnsOneSessionPerWeek()
    {
        // Four Tuesdays: the 3rd, 10th, 17th and 24th of March 2026.
        var sessions = SessionPlanner.Plan(
            [Slot(DayOfWeek.Tuesday, 18, 19)],
            Monday,
            Monday.AddDays(27));

        sessions.Should().HaveCount(4);
        sessions.Select(session => session.Date).Should().Equal(
            Tuesday, Tuesday.AddDays(7), Tuesday.AddDays(14), Tuesday.AddDays(21));
    }

    [Fact]
    public void Plan_TwoSlotsOnDifferentDays_ReturnsBothEveryWeekTheyFallIn()
    {
        var sessions = SessionPlanner.Plan(
            [Slot(DayOfWeek.Tuesday, 18, 19), Slot(DayOfWeek.Thursday, 18, 19)],
            Monday,
            Monday.AddDays(6)); // one week

        sessions.Should().HaveCount(2);
        sessions.Select(session => session.Date).Should().Equal(Tuesday, Thursday);
    }

    [Fact]
    public void Plan_TwoSlotsOnTheSameDay_ReturnsBothOrderedByStartTime()
    {
        // A morning and an evening group sharing a day — SessionPlanner does
        // not reject this; see WeeklySlot's own remarks for why that is a
        // schedule-editing decision, not this function's.
        var sessions = SessionPlanner.Plan(
            [Slot(DayOfWeek.Tuesday, 18, 19), Slot(DayOfWeek.Tuesday, 8, 9)],
            Tuesday,
            Tuesday);

        sessions.Should().HaveCount(2);
        sessions[0].StartsAt.Should().Be(new TimeOnly(8, 0));
        sessions[1].StartsAt.Should().Be(new TimeOnly(18, 0));
    }

    [Fact]
    public void Plan_SlotsGivenOutOfOrder_StillOrdersResultsByDateThenTime()
    {
        var sessions = SessionPlanner.Plan(
            [Slot(DayOfWeek.Thursday, 18, 19), Slot(DayOfWeek.Tuesday, 18, 19)],
            Monday,
            Monday.AddDays(6));

        sessions.Select(session => session.Date).Should().Equal(Tuesday, Thursday);
    }
}
