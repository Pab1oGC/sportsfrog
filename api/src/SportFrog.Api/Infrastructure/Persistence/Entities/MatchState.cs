using System.Text.Json.Serialization;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// What has become of a fixture.
/// </summary>
/// <remarks>
/// Six states, and the last three are the reason this is not a boolean. A
/// match that was never played is not one result: a postponement will be
/// played later and keeps its place in the table as a blank, a walkover was
/// awarded without anyone playing and counts as a result, and a cancellation
/// is neither and never will be. Collapsing them would make the standings
/// wrong in three different ways.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<MatchState>))]
public enum MatchState
{
    /// <summary>On the calendar, not yet started.</summary>
    Scheduled,

    /// <summary>Being played right now.</summary>
    InProgress,

    /// <summary>Played to the end, with a score.</summary>
    Finished,

    /// <summary>Not played, and to be rescheduled. It still owes a result.</summary>
    Postponed,

    /// <summary>Awarded because a side did not appear. A result without a match.</summary>
    Walkover,

    /// <summary>Struck from the calendar. It owes nothing and produces nothing.</summary>
    Cancelled,
}
