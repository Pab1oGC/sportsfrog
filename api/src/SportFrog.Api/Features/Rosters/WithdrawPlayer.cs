using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Takes a player out of a squad, or puts them back.
/// </summary>
/// <remarks>
/// The sporting removal, and the one that is nearly always right. It says the
/// player left this team, keeps everything recorded while they were on it,
/// and puts their shirt back into circulation.
///
/// Reversible, because a withdrawal is a claim about the world and claims are
/// sometimes entered wrongly. Putting somebody back is not a formality
/// though: while they were gone the squad may have filled up and their number
/// may have been handed to someone else, so re-entering is checked exactly as
/// a first registration is.
/// </remarks>
public static class WithdrawPlayer
{
    public sealed record Request(bool Withdrawn);

    public static IEndpointRouteBuilder MapWithdrawPlayer(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/roster/{id:guid}/withdrawal", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(WithdrawPlayer))
            .WithSummary("Withdraws a player from a squad, or reinstates them.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        RosterPolicy policy,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var entry = await database.RosterEntries
            .Include(candidate => candidate.Team)
                .ThenInclude(team => team!.Category)
            .Include(candidate => candidate.Athlete)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (entry?.Team is not { Category: { } category } team || entry.Athlete is not { } athlete)
        {
            return Results.NotFound();
        }

        if (request.Withdrawn == entry.WithdrawnAt.HasValue)
        {
            // Already where the caller is asking for. Answered as success: a
            // second click on "withdraw" is not a mistake to report.
            return Results.NoContent();
        }

        if (!request.Withdrawn)
        {
            // Coming back is a registration again. The squad may have filled
            // while they were away and the shirt may have been reissued, so
            // the same rules answer both.
            var violations = await policy.InspectAsync(
                team, category, athlete, entry.JerseyNumber, excluding: id, cancellationToken);

            if (violations.Count > 0)
            {
                return Results.ValidationProblem(violations
                    .GroupBy(violation => violation.Property)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(violation => violation.Message).ToArray()));
            }
        }

        // From the same clock the rest of the application is tested against,
        // and recorded rather than merely flagged: when someone left decides
        // which matches they were available for.
        entry.WithdrawnAt = request.Withdrawn ? clock.GetUtcNow() : null;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Reinstating into a shirt somebody claimed at the same moment.
            // The index counts only players actually on the team, which is
            // exactly the set this rejoins.
            return Results.Problem(
                detail: "That shirt was taken at the same moment. Read the squad and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
