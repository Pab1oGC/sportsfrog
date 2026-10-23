using AwesomeAssertions;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Domain.Tests.Scheduling;

/// <summary>
/// Whether a requested jornada start is one <see cref="ScheduleCalendar"/>'s
/// own caller should ever be allowed to write to the calendar.
/// </summary>
public sealed class JornadaWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Inspect_StartAfterNowAndNoPriorJornada_IsNotAViolation()
    {
        var requestedStart = Now.AddDays(1);

        JornadaWindow.Inspect(requestedStart, Now, latestScheduled: null)
            .Should().Be(JornadaWindow.Violation.None);
    }

    [Fact]
    public void Inspect_StartExactlyNow_IsNotAViolation()
    {
        // Equal is not "earlier than" — the boundary itself is still fine,
        // and this is the case a frozen clock in a real request would hit.
        JornadaWindow.Inspect(Now, Now, latestScheduled: null)
            .Should().Be(JornadaWindow.Violation.None);
    }

    [Fact]
    public void Inspect_StartBeforeNow_IsInThePast()
    {
        var requestedStart = Now.AddMinutes(-1);

        JornadaWindow.Inspect(requestedStart, Now, latestScheduled: null)
            .Should().Be(JornadaWindow.Violation.InThePast);
    }

    [Fact]
    public void Inspect_StartBeforeAnAlreadyScheduledJornada_IsBeforeAnExistingJornada()
    {
        var latestScheduled = Now.AddDays(10);
        var requestedStart = Now.AddDays(5); // after now, but behind the category's own last jornada

        JornadaWindow.Inspect(requestedStart, Now, latestScheduled)
            .Should().Be(JornadaWindow.Violation.BeforeAnExistingJornada);
    }

    [Fact]
    public void Inspect_StartExactlyOnTheExistingJornada_IsNotAViolation()
    {
        var latestScheduled = Now.AddDays(10);

        JornadaWindow.Inspect(latestScheduled, Now, latestScheduled)
            .Should().Be(JornadaWindow.Violation.None);
    }

    [Fact]
    public void Inspect_StartAfterTheExistingJornadaAndAfterNow_IsNotAViolation()
    {
        var latestScheduled = Now.AddDays(10);
        var requestedStart = Now.AddDays(11);

        JornadaWindow.Inspect(requestedStart, Now, latestScheduled)
            .Should().Be(JornadaWindow.Violation.None);
    }

    [Fact]
    public void Inspect_InThePastTakesPriorityOverBeforeAnExistingJornada()
    {
        // A start that fails both checks is reported as the past -- the more
        // fundamental reason it cannot be placed, and the one every caller
        // reaches first regardless of whether this category has ever had a
        // jornada scheduled at all.
        var latestScheduled = Now.AddDays(-5);
        var requestedStart = Now.AddDays(-10);

        JornadaWindow.Inspect(requestedStart, Now, latestScheduled)
            .Should().Be(JornadaWindow.Violation.InThePast);
    }
}
