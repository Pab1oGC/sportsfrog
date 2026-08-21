using System.Text.Json.Serialization;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// How much detail is recorded while a match is played.
/// </summary>
/// <remarks>
/// A choice about effort, not about features. Under <see cref="Basic"/> a
/// match records its score and nothing else, which is all a volunteer with a
/// phone at a municipal field can reasonably keep up with. Under
/// <see cref="Detailed"/> every event is attributed to a player, which is
/// what makes a top scorer table or a card count possible.
///
/// Fixed per competition rather than per match, so a table is not built from
/// halves that counted different things.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<CaptureLevel>))]
public enum CaptureLevel
{
    /// <summary>The score, and who won.</summary>
    Basic,

    /// <summary>Every event attributed to the player it belongs to.</summary>
    Detailed,
}
