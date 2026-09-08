using AwesomeAssertions;
using SportFrog.Domain.Performances;

namespace SportFrog.Domain.Tests.Performances;

/// <summary>
/// Decides who advances from a finished classification stage into a
/// knockout. Expected results below are worked out from what a
/// classification-based cutoff means — everyone scored, ranked, a tie at the
/// line never split — the same rules ClassificationRankingTests already
/// establishes for the ranking itself, not from reading Build's private
/// steps and mirroring what they currently do.
/// </summary>
public sealed class ClassificationAdvancementTests
{
    private static PerformanceEntry Scored(string team, int score) =>
        new(Guid.NewGuid(), Guid.NewGuid(), team, PerformanceStatus.Scored, score);

    private static PerformanceEntry Pending(string team) =>
        new(Guid.NewGuid(), Guid.NewGuid(), team, PerformanceStatus.Pending, null);

    [Fact]
    public void Build_NoPerformances_IsRefused()
    {
        var (plan, problem) = ClassificationAdvancement.Build([], qualifiers: 2);

        plan.Should().BeNull();
        problem.Should().NotBeNull();
    }

    [Fact]
    public void Build_SomeoneNotYetScored_IsRefused()
    {
        var entries = new[] { Scored("A", 900), Pending("B") };

        var (plan, problem) = ClassificationAdvancement.Build(entries, qualifiers: 2);

        plan.Should().BeNull();
        problem.Should().NotBeNull();
    }

    [Fact]
    public void Build_FewerThanTwoWouldQualify_IsRefused()
    {
        var entries = new[] { Scored("A", 900), Scored("B", 800) };

        var (plan, problem) = ClassificationAdvancement.Build(entries, qualifiers: 1);

        plan.Should().BeNull();
        problem.Should().NotBeNull();
    }

    [Fact]
    public void Build_ValidField_SeedsStrongestFirst()
    {
        var entries = new[] { Scored("A", 700), Scored("B", 900), Scored("C", 800), Scored("D", 600) };

        var (plan, _) = ClassificationAdvancement.Build(entries, qualifiers: 4);

        plan.Should().NotBeNull();
        plan!.Seeded.Should().HaveCount(4);
        // B (900) first, D (600) last — the exact score order, since nothing
        // here reorders it the way GroupStageAdvancement's Reseed does.
        var byId = entries.ToDictionary(entry => entry.TeamId, entry => entry.TeamName);
        plan.Seeded.Select(id => byId[id]).Should().ContainInOrder("B", "C", "A", "D");
    }

    [Fact]
    public void Build_CutoffMidField_TakesOnlyTheTopQualifiers()
    {
        var entries = new[] { Scored("A", 900), Scored("B", 800), Scored("C", 700), Scored("D", 600) };

        var (plan, _) = ClassificationAdvancement.Build(entries, qualifiers: 2);

        plan!.Seeded.Should().HaveCount(2);
    }

    [Fact]
    public void Build_TieAtTheCutoff_NeverSplitsIt()
    {
        // Two qualifiers requested; B and C are tied for the second slot.
        // Both go through, exactly as ClassificationRankingTests already
        // shows for the ranking underneath this.
        var entries = new[] { Scored("A", 900), Scored("B", 800), Scored("C", 800), Scored("D", 700) };

        var (plan, _) = ClassificationAdvancement.Build(entries, qualifiers: 2);

        plan!.Seeded.Should().HaveCount(3);
    }

    [Fact]
    public void Build_QualifiersExceedsTheField_TakesEveryone()
    {
        var entries = new[] { Scored("A", 900), Scored("B", 800) };

        var (plan, _) = ClassificationAdvancement.Build(entries, qualifiers: 10);

        plan!.Seeded.Should().HaveCount(2);
    }
}
