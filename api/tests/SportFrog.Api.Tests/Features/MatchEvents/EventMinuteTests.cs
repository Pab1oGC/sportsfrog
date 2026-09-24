using AwesomeAssertions;
using SportFrog.Api.Features.MatchEvents;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.MatchEvents;

/// <summary>
/// Whether the minute of an event is one its period can reach. The case that
/// started this: minute 99 could be filed under the first half.
/// </summary>
public sealed class EventMinuteTests
{
    private static EventPolicy.MatchContext Context(
        ScoreMode mode = ScoreMode.Cumulative,
        short? minutes = 45,
        short count = 2,
        short? extra = null) =>
        new(
            new Match(),
            "football",
            mode,
            CaptureLevel.Detailed,
            new RulesetConfiguration
            {
                Periods = new PeriodRules
                {
                    Count = count, Label = "tiempo", Minutes = minutes, MaxExtraMinutes = extra,
                },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            });

    private static List<EventViolation> Inspect(EventPolicy.MatchContext context, short? period, short? minute)
    {
        var violations = new List<EventViolation>();
        EventPolicy.InspectMinute(context, period, minute, violations);
        return violations;
    }

    [Fact]
    public void Minute99_UnderTheFirstHalf_IsRefused()
    {
        var violations = Inspect(Context(), period: 1, minute: 99);

        violations.Should().ContainSingle().Which.Property.Should().Be("Minute");
    }

    [Fact]
    public void Minute99_UnderTheSecondHalf_IsAccepted()
    {
        // 90+9: past the regular time and inside the stoppage allowance.
        Inspect(Context(), period: 2, minute: 99).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 45)]
    [InlineData(1, 48)]   // 45+3
    [InlineData(1, 65)]   // 45+20, the extreme
    [InlineData(2, 46)]
    [InlineData(2, 90)]
    [InlineData(2, 110)]  // 90+20
    public void MinutesInsideThePeriodsWindow_AreAccepted(short period, short minute)
    {
        Inspect(Context(), period, minute).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 66)]   // past 45+20
    [InlineData(2, 45)]   // still the first half's minute
    [InlineData(2, 111)]  // past 90+20
    public void MinutesOutsideThePeriodsWindow_AreRefused(short period, short minute)
    {
        Inspect(Context(), period, minute).Should().ContainSingle();
    }

    [Fact]
    public void ThePeriodLengthComesFromTheReglamento()
    {
        // Youth football in 30-minute halves: the first half reaches 30+20 = 50,
        // so 51 is past it, while 46 is 30+16 — still stoppage time.
        var thirties = Context(minutes: 30);

        Inspect(thirties, period: 1, minute: 46).Should().BeEmpty();
        Inspect(thirties, period: 1, minute: 51).Should().ContainSingle();
        Inspect(thirties, period: 2, minute: 46).Should().BeEmpty();
        Inspect(thirties, period: 2, minute: 30).Should().ContainSingle();
    }

    [Fact]
    public void TheStoppageAllowanceComesFromTheReglamento()
    {
        var tenOnly = Context(extra: 10);

        Inspect(tenOnly, period: 1, minute: 55).Should().BeEmpty();
        Inspect(tenOnly, period: 1, minute: 56).Should().ContainSingle();
    }

    [Fact]
    public void WithoutAPeriod_TheMinuteIsHeldToTheMatchAsAWhole()
    {
        Inspect(Context(), period: null, minute: 110).Should().BeEmpty();
        Inspect(Context(), period: null, minute: 111).Should().ContainSingle();
    }

    [Fact]
    public void NoMinute_IsNotChecked()
    {
        Inspect(Context(), period: 1, minute: null).Should().BeEmpty();
    }

    [Fact]
    public void ASportDecidedInSets_IsNotChecked()
    {
        // A taekwondo bout: the clock restarts with each asalto, so
        // "cumulative minute 30 of the third round" is not a thing to enforce.
        Inspect(Context(ScoreMode.Sets, minutes: 2, count: 3), period: 1, minute: 30).Should().BeEmpty();
    }

    [Fact]
    public void APeriodWithoutAClock_IsNotChecked()
    {
        Inspect(Context(minutes: null), period: 1, minute: 99).Should().BeEmpty();
    }
}
