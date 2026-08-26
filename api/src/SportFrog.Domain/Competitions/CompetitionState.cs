using System.Text.Json.Serialization;

namespace SportFrog.Domain.Competitions;

/// <summary>
/// Where a competition stands in its own life.
/// </summary>
/// <remarks>
/// A database enum rather than text, like membership_role and unlike a
/// sport's score mode: the set is closed by the design, every value is
/// reached through a transition the application decides, and adding one is a
/// change to how competitions work rather than a new option in a catalog.
///
/// Both halves of the mapping live in <see cref="SportFrogDataSource"/>. The
/// driver has to be taught to read and write the labels, and EF has to be
/// taught the column is not an integer; declaring only one of them compiles
/// and then fails at the first write.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<CompetitionState>))]
public enum CompetitionState
{
    /// <summary>Being set up. Nothing has been published and nothing is played.</summary>
    Draft,

    /// <summary>Fixtures exist and are announced, but no match has started.</summary>
    Scheduled,

    /// <summary>Under way.</summary>
    InProgress,

    /// <summary>Played to the end. Its results are history.</summary>
    Finished,

    /// <summary>Abandoned. Kept, because what was played still happened.</summary>
    Cancelled,
}
