using AwesomeAssertions;
using SportFrog.Api.Features.MatchEvents;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.MatchEvents;

/// <summary>
/// Whether the period an event names is one the match has — and, under a
/// sport decided in sets, whether it was named at all. The case that
/// started this: a volleyball point with no set recorded could not say
/// which set it belonged to, and nothing stopped it being filed anyway
/// (see <c>AddVolleyballScoringMetrics</c> and
/// <see cref="SportFrog.Domain.Matches.LiveScore.ComputePlayedPeriods"/>).
/// </summary>
public sealed class EventPeriodTests
{
    private static EventPolicy.MatchContext Context(ScoreMode mode, short count = 2, string label = "tiempo") =>
        new(
            new Match(),
            mode == ScoreMode.Sets ? "volleyball" : "football",
            mode,
            CaptureLevel.Detailed,
            new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = count, Label = label, Minutes = mode == ScoreMode.Cumulative ? 45 : null },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            });

    private static List<EventViolation> Inspect(EventPolicy.MatchContext context, short? period)
    {
        var violations = new List<EventViolation>();
        EventPolicy.InspectPeriod(context, period, violations);
        return violations;
    }

    [Fact]
    public void Cumulative_NoPeriod_IsAccepted()
    {
        // A goal still counts towards the total without one — the period is
        // a detail of when it happened, not something the score depends on.
        Inspect(Context(ScoreMode.Cumulative), period: null).Should().BeEmpty();
    }

    [Fact]
    public void Cumulative_APeriodTheMatchHas_IsAccepted()
    {
        Inspect(Context(ScoreMode.Cumulative, count: 2), period: 2).Should().BeEmpty();
    }

    [Fact]
    public void Cumulative_APeriodTheMatchDoesNotHave_IsRejected()
    {
        Inspect(Context(ScoreMode.Cumulative, count: 2), period: 3)
            .Should().ContainSingle().Which.Property.Should().Be("PeriodNumber");
    }

    [Fact]
    public void Sets_NoPeriod_IsRejected()
    {
        // Unlike under a running clock, this is not a missing detail: it is
        // a point nobody can ever say which set it decided.
        Inspect(Context(ScoreMode.Sets, count: 5, label: "set"), period: null)
            .Should().ContainSingle().Which.Property.Should().Be("PeriodNumber");
    }

    [Fact]
    public void Sets_APeriodTheMatchHas_IsAccepted()
    {
        Inspect(Context(ScoreMode.Sets, count: 5, label: "set"), period: 3).Should().BeEmpty();
    }

    [Fact]
    public void Sets_APeriodTheMatchDoesNotHave_IsRejected()
    {
        Inspect(Context(ScoreMode.Sets, count: 5, label: "set"), period: 6)
            .Should().ContainSingle().Which.Property.Should().Be("PeriodNumber");
    }

    [Fact]
    public void Judged_NoPeriod_IsAccepted()
    {
        // Out of this rule's scope: a judged performance is not derived
        // period by period, so there is nothing here for a missing one to
        // put at risk.
        Inspect(Context(ScoreMode.Judged, count: 1, label: "actuación"), period: null).Should().BeEmpty();
    }
}
