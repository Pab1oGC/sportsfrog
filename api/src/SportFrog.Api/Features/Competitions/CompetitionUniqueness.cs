using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// What makes two competitions of one organization "the same": their public
/// address and their name. Kept in one place so setting a competition up and
/// correcting one cannot drift into answering the same collision two ways.
/// </summary>
/// <remarks>
/// Each rule has two halves that answer the same question at different times.
/// The pre-check here is the ordinary case and gives the caller a precise
/// message before anything is written; the unique index behind it
/// (<c>uq_competitions_slug</c>, <c>uq_competitions_name</c>) is what holds
/// under a race, and <see cref="ConflictFor"/> turns its violation into the
/// same answer the pre-check would have given.
/// </remarks>
internal static class CompetitionUniqueness
{
    private const string NameIndex = "uq_competitions_name";

    /// <summary>
    /// Whether a living competition already goes by this name, ignoring case
    /// to match the index. <paramref name="except"/> is the competition being
    /// corrected, which is not a collision with itself.
    /// </summary>
    /// <remarks>
    /// Compared as <c>lower(name)</c> on both sides, exactly the expression
    /// the index is built on, so the pre-check and the index agree on what
    /// counts as a repeat.
    /// </remarks>
    public static Task<bool> IsNameTakenAsync(
        SportFrogDbContext database,
        string name,
        Guid? except,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();

        return database.Competitions.AnyAsync(
            competition => competition.Id != except
                           && competition.Name.ToLower() == normalized,
            cancellationToken);
    }

    public static IResult NameTaken() => Results.Problem(
        detail: "Ya hay una competencia con ese nombre.",
        statusCode: StatusCodes.Status409Conflict);

    public static IResult SlugTaken() => Results.Problem(
        detail: "Ya hay una competencia usando esa dirección.",
        statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// The answer for a save that lost a race against another one, or
    /// <c>null</c> when the failure was not a unique violation and is not this
    /// class's to explain.
    /// </summary>
    /// <remarks>
    /// Anything that is a unique violation and is not the name index is taken
    /// to be the slug's: it is the only other unique index the table carries,
    /// and it is what this answered before the name had one.
    /// </remarks>
    public static IResult? ConflictFor(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            ? violation.ConstraintName == NameIndex ? NameTaken() : SlugTaken()
            : null;
}
