using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Moves several fixtures in one step, validated as the arrangement they end
/// up in together rather than one at a time.
/// </summary>
/// <remarks>
/// <see cref="RescheduleMatch"/> already moves one fixture, and correctly
/// refuses a destination something else already holds. That correctness is
/// exactly what makes reordering several by hand tedious: swapping fixture A
/// and fixture B one request at a time always tries to put A where B still
/// is, and gets refused, until whoever is doing it works out that B has to
/// move first, to somewhere free, before A can take its place. A three-way
/// rotation needs the same puzzle solved for three.
///
/// This solves the puzzle by not asking the database to referee it one move
/// at a time. Every fixture the batch could possibly touch — every one being
/// moved, and every other live fixture sharing a ground or a team with one of
/// them — is read once, the *final* arrangement is checked as a whole (see
/// <see cref="BulkRescheduleConflicts"/>), and only if none of it collides
/// with any of the rest does anything get written. A swap that would fail
/// two different ways if attempted one fixture at a time succeeds here in
/// one, because nothing is ever compared against a half-applied state.
///
/// Teams are deliberately not part of this: correcting who plays whom is
/// <see cref="RescheduleMatch"/>'s job, one fixture at a time, and mixing it
/// into a batch would multiply <see cref="FixturePolicy"/>'s team-eligibility
/// checks across every move for a case nothing asked for.
///
/// "Every other live fixture sharing a team" reaches further than team ids:
/// a person entered under a second, unrelated <c>Team</c> row — a different
/// category, a different competition entirely — is still one person, so the
/// scope this batch checks against grows to every team fielding one of the
/// same roster athletes before <see cref="BulkRescheduleConflicts"/> ever
/// runs. Not competition-scoped for the same reason FixturePolicy's own
/// collision check is not: a competitor cannot be in two places at once
/// regardless of which competition either fixture belongs to.
/// </remarks>
public static class RescheduleMatchesBulk
{
    public sealed record MatchMove(Guid MatchId, Guid? VenueSpaceId, DateTimeOffset? ScheduledAt, short? RoundNumber);

    public sealed record Request(IReadOnlyList<MatchMove> Moves);

    public sealed record Response(int Moved);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Moves)
                .NotEmpty().WithMessage("Hay que indicar al menos un partido para mover.");

            RuleFor(request => request.Moves)
                .Must(moves => moves.Select(move => move.MatchId).Distinct().Count() == moves.Count)
                .When(request => request.Moves.Count > 0)
                .WithMessage("El mismo partido está repetido en la lista.");

            RuleForEach(request => request.Moves).ChildRules(move =>
            {
                move.RuleFor(m => m.RoundNumber)
                    .InclusiveBetween((short)1, (short)200)
                    .When(m => m.RoundNumber.HasValue)
                    .WithMessage("El número de ronda está entre 1 y 200.");
            });
        }
    }

    public static IEndpointRouteBuilder MapRescheduleMatchesBulk(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/matches/reschedule-bulk", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RescheduleMatchesBulk))
            .WithSummary("Moves several fixtures in one step, validated as a whole arrangement.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        OrganizationContext organization,
        IBackgroundJobClient jobs,
        CancellationToken cancellationToken)
    {
        var moveIds = request.Moves.Select(move => move.MatchId).ToList();

        var matches = await database.Matches
            .Where(match => moveIds.Contains(match.Id))
            .ToDictionaryAsync(match => match.Id, cancellationToken);

        var byIndex = new Dictionary<int, List<string>>();

        void AddViolation(int index, string message)
        {
            if (!byIndex.TryGetValue(index, out var messages))
            {
                byIndex[index] = messages = [];
            }

            messages.Add(message);
        }

        IResult? AsValidationProblem() => byIndex.Count == 0
            ? null
            : Results.ValidationProblem(byIndex.ToDictionary(
                entry => $"Moves[{entry.Key}]", entry => entry.Value.ToArray()));

        for (var i = 0; i < request.Moves.Count; i++)
        {
            if (!matches.ContainsKey(request.Moves[i].MatchId))
            {
                AddViolation(i, "Ningún partido de esta organización tiene ese identificador.");
            }
        }

        if (AsValidationProblem() is { } missingProblem)
        {
            return missingProblem;
        }

        // Every space named by the batch has to exist and be usable — the
        // same rule FixturePolicy.InspectSpaceAsync applies one fixture at a
        // time, checked once per distinct space instead of once per move.
        var spaceIds = request.Moves
            .Where(move => move.VenueSpaceId is not null)
            .Select(move => move.VenueSpaceId!.Value)
            .Distinct()
            .ToList();

        var usableSpaces = spaceIds.Count == 0
            ? []
            : await database.VenueSpaces
                .AsNoTracking()
                .Where(space => spaceIds.Contains(space.Id) && space.IsActive && space.Venue!.IsActive)
                .Select(space => space.Id)
                .ToListAsync(cancellationToken);

        for (var i = 0; i < request.Moves.Count; i++)
        {
            var spaceId = request.Moves[i].VenueSpaceId;

            if (spaceId is not null && !usableSpaces.Contains(spaceId.Value))
            {
                AddViolation(i, "Ese espacio no existe o no está disponible.");
            }
        }

        if (AsValidationProblem() is { } spaceProblem)
        {
            return spaceProblem;
        }

        // Team names are cosmetic — only for the message a conflict prints —
        // so they are read separately and only for the teams this batch
        // actually involves, rather than widening the query above.
        var teamIds = matches.Values.SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId }).ToHashSet();

        var teamNames = await database.Teams
            .AsNoTracking()
            .Where(team => teamIds.Contains(team.Id))
            .ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken);

        // Who is currently on these teams — and, from there, every other
        // team row anywhere that fields one of the same people under a
        // different registration. A team is scoped to one category; a
        // person is not, so this is the only way to see that two fixtures
        // in different competitions still cannot both go ahead.
        var myAthleteIds = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => teamIds.Contains(entry.TeamId) && entry.WithdrawnAt == null)
            .Select(entry => entry.AthleteId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var sharingTeamIds = myAthleteIds.Count == 0
            ? []
            : await database.RosterEntries
                .AsNoTracking()
                .Where(entry => myAthleteIds.Contains(entry.AthleteId) && entry.WithdrawnAt == null)
                .Select(entry => entry.TeamId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var relevantTeamIds = teamIds.Union(sharingTeamIds).ToList();

        var athleteIdsByTeam = (await database.RosterEntries
            .AsNoTracking()
            .Where(entry => relevantTeamIds.Contains(entry.TeamId) && entry.WithdrawnAt == null)
            .Select(entry => new { entry.TeamId, entry.AthleteId })
            .ToListAsync(cancellationToken))
            .GroupBy(entry => entry.TeamId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<Guid>)group.Select(entry => entry.AthleteId).ToHashSet());

        HashSet<Guid> AthletesOf(Guid homeTeamId, Guid awayTeamId) =>
        [
            .. athleteIdsByTeam.GetValueOrDefault(homeTeamId, []),
            .. athleteIdsByTeam.GetValueOrDefault(awayTeamId, []),
        ];

        var moved = new List<(int Index, ScheduledSlot Slot)>(request.Moves.Count);

        for (var i = 0; i < request.Moves.Count; i++)
        {
            var move = request.Moves[i];
            var match = matches[move.MatchId];

            moved.Add((i, new ScheduledSlot(
                match.Id,
                match.HomeTeamId, teamNames.GetValueOrDefault(match.HomeTeamId, "?"),
                match.AwayTeamId, teamNames.GetValueOrDefault(match.AwayTeamId, "?"),
                move.VenueSpaceId, move.ScheduledAt,
                AthletesOf(match.HomeTeamId, match.AwayTeamId))));
        }

        // Every other live fixture that could possibly collide with one of
        // the moves: sharing a ground the batch touches, a team the batch
        // touches, or a team fielding one of the same people under another
        // registration. Everything else on the calendar is irrelevant to
        // whether this particular batch fits.
        var others = await database.Matches
            .AsNoTracking()
            .Where(match => !moveIds.Contains(match.Id))
            .Where(match => match.ScheduledAt != null)
            .Where(match => match.Status != MatchState.Cancelled && match.Status != MatchState.Postponed)
            .Where(match => (match.VenueSpaceId != null && spaceIds.Contains(match.VenueSpaceId.Value))
                || relevantTeamIds.Contains(match.HomeTeamId) || relevantTeamIds.Contains(match.AwayTeamId))
            .Select(match => new
            {
                match.Id,
                match.HomeTeamId,
                HomeTeamName = match.HomeTeam!.Name,
                match.AwayTeamId,
                AwayTeamName = match.AwayTeam!.Name,
                match.VenueSpaceId,
                match.ScheduledAt,
            })
            .ToListAsync(cancellationToken);

        var conflicts = BulkRescheduleConflicts.Find(
            moved,
            [.. others.Select(match => new ScheduledSlot(
                match.Id, match.HomeTeamId, match.HomeTeamName, match.AwayTeamId, match.AwayTeamName,
                match.VenueSpaceId, match.ScheduledAt, AthletesOf(match.HomeTeamId, match.AwayTeamId)))]);

        if (conflicts.Count > 0)
        {
            return Results.ValidationProblem(conflicts.ToDictionary(
                entry => $"Moves[{entry.Key}]", entry => entry.Value.ToArray()));
        }

        // Captured before anything is overwritten: whether a move actually
        // changes where or when a fixture is, as opposed to only its round —
        // the only case worth writing to a club about.
        var changedMatchIds = new List<Guid>();

        foreach (var move in request.Moves)
        {
            var match = matches[move.MatchId];

            if (match.VenueSpaceId != move.VenueSpaceId || match.ScheduledAt != move.ScheduledAt)
            {
                changedMatchIds.Add(match.Id);
            }

            match.VenueSpaceId = move.VenueSpaceId;
            match.ScheduledAt = move.ScheduledAt;
            match.RoundNumber = move.RoundNumber;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Only reachable if another request landed on one of these same
            // grounds in the instant between the check above and this write
            // — the same race FixturePolicy's own collision check cannot
            // close either, and uq_space_schedule is what actually closes it.
            return Results.Problem(
                detail: "Algún espacio se ocupó justo ahora, entre que se revisó y que se guardó. " +
                        "Volvé a intentar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (changedMatchIds.Count > 0)
        {
            Notify(context, jobs, organization.RequireOrganizationId(), organization.UserId ?? Guid.Empty, changedMatchIds);
        }

        return Results.Ok(new Response(request.Moves.Count));
    }

    /// <summary>
    /// Queued once the response has actually gone out — see <see cref="RescheduleMatch"/>'s
    /// own remarks on why. One job per fixture rather than one job for the
    /// whole batch, so a relay unreachable for one notice does not also hold
    /// up — or, on Hangfire's own retry, resend — every other club's.
    /// </summary>
    private static void Notify(
        HttpContext context, IBackgroundJobClient jobs, Guid organizationId, Guid userId, List<Guid> matchIds) =>
        context.Response.OnCompleted(() =>
        {
            foreach (var matchId in matchIds)
            {
                jobs.Enqueue<MatchRescheduleNotificationJob>(
                    job => job.RunAsync(organizationId, userId, matchId, CancellationToken.None));
            }

            return Task.CompletedTask;
        });
}
