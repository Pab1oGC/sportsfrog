using AwesomeAssertions;
using SportFrog.Api.Features.Matches;
using SportFrog.Domain.Matches;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// Whether a fixture PDF request is a "whole sheet" that has to be gap-free
/// before it prints, or a single jornada that never had that requirement.
/// </summary>
public sealed class ReadMatchesTests
{
    private static ReadMatches.Summary Match(DateTimeOffset? at, MatchState status = MatchState.Scheduled) =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Sub-15",
            Guid.NewGuid(), "Equipo A", Guid.NewGuid(), "Equipo B",
            null, null, null,
            at, 1, null, status, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void RefuseIfIncomplete_WholeSheetEverythingScheduled_Allows()
    {
        var matches = new[]
        {
            Match(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)),
            Match(new DateTimeOffset(2026, 5, 8, 10, 0, 0, TimeSpan.Zero)),
        };

        ReadMatches.RefuseIfIncomplete(round: null, matches).Should().BeNull();
    }

    [Fact]
    public void RefuseIfIncomplete_WholeSheetOneMatchUndated_Refuses()
    {
        var matches = new[]
        {
            Match(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)),
            Match(at: null),
        };

        ReadMatches.RefuseIfIncomplete(round: null, matches).Should().NotBeNull();
    }

    [Fact]
    public void RefuseIfIncomplete_UndatedButCancelled_IsNotAGap_Allows()
    {
        // A cancelled match owes nothing further — it never gets a date, and
        // that is expected rather than a hole in the sheet.
        var matches = new[]
        {
            Match(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)),
            Match(at: null, status: MatchState.Cancelled),
        };

        ReadMatches.RefuseIfIncomplete(round: null, matches).Should().BeNull();
    }

    [Fact]
    public void RefuseIfIncomplete_SingleJornadaWithAnUndatedMatch_StillAllows()
    {
        // A jornada PDF was never gated on completeness — a fixture that did
        // not fit when the calendar was generated still deserves a PDF of
        // whatever the rest of that jornada does have.
        var matches = new[] { Match(at: null) };

        ReadMatches.RefuseIfIncomplete(round: 3, matches).Should().BeNull();
    }

    [Fact]
    public void RefuseIfIncomplete_NoMatchesAtAll_Allows()
    {
        ReadMatches.RefuseIfIncomplete(round: null, matches: []).Should().BeNull();
    }
}
