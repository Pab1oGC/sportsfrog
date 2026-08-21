using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Writes an enum as the same label the database stores it under.
/// </summary>
/// <remarks>
/// Applied to the enum type itself rather than configured globally, so a
/// value keeps its wire form wherever it appears — inside a summary, nested
/// in a list, embedded in a settings document — without every endpoint having
/// to remember to convert it.
///
/// Without it these arrive as integers, and an integer is the worst of both:
/// it means nothing to whoever reads the response, and it silently changes
/// meaning the day a member is inserted into the middle of the enum.
///
/// Snake case because that is what the database labels are and what the rest
/// of this API already answers with — in_progress, not InProgress.
///
/// Reading is left case-insensitive by the base converter, but nothing here
/// depends on that: request contracts take these as plain strings and parse
/// them under a validator, so an unrecognized value is answered as a
/// validation failure rather than as a body that could not be read.
/// </remarks>
public sealed class SnakeCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(JsonNamingPolicy.SnakeCaseLower)
    {
    }
}
