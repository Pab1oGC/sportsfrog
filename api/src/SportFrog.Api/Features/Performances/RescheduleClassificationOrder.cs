using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Sets the running order of several performances at once, validated as the
/// arrangement they end up in together — not one at a time.
/// </summary>
/// <remarks>
/// The running-order sibling of <see cref="Matches.RescheduleMatchesBulk"/>:
/// arranging a whole mat's running order by hand, one <see cref="SchedulePerformance"/>
/// call per team, hits the same puzzle a fixture swap does — putting team A on
/// team B's turn is refused while B still holds it, and reordering three or
/// more needs the same puzzle solved for all of them together. This solves it
/// the same way: every move plus every other live performance sharing a mat
/// the batch touches is read once, the arrangement is checked as a whole (see
/// <see cref="PerformanceOrderConflicts"/>), and only if none of it collides
/// does anything get written.
/// </remarks>
public static class RescheduleClassificationOrder
{
    public sealed record PerformanceMove(Guid PerformanceId, Guid? VenueSpaceId, DateOnly? ScheduledOn, short? OrderNumber);

    public sealed record Request(IReadOnlyList<PerformanceMove> Moves);

    public sealed record Response(int Moved);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Moves)
                .NotEmpty().WithMessage("Hay que indicar al menos una actuación para ubicar.");

            RuleFor(request => request.Moves)
                .Must(moves => moves.Select(move => move.PerformanceId).Distinct().Count() == moves.Count)
                .When(request => request.Moves.Count > 0)
                .WithMessage("La misma actuación está repetida en la lista.");

            RuleForEach(request => request.Moves).ChildRules(move =>
            {
                move.RuleFor(m => m.OrderNumber)
                    .InclusiveBetween((short)1, (short)999)
                    .When(m => m.OrderNumber.HasValue)
                    .WithMessage("El turno está entre 1 y 999.");
            });
        }
    }

    public static IEndpointRouteBuilder MapRescheduleClassificationOrder(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/performances/reschedule-bulk", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RescheduleClassificationOrder))
            .WithSummary("Sets the running order of several performances at once, validated as a whole arrangement.");

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
        var moveIds = request.Moves.Select(move => move.PerformanceId).ToList();

        var performances = await database.Performances
            .Where(performance => moveIds.Contains(performance.Id))
            .ToDictionaryAsync(performance => performance.Id, cancellationToken);

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
            if (!performances.ContainsKey(request.Moves[i].PerformanceId))
            {
                AddViolation(i, "Ninguna actuación de esta organización tiene ese identificador.");
            }
        }

        if (AsValidationProblem() is { } missingProblem)
        {
            return missingProblem;
        }

        // Every space named by the batch has to exist and be usable — same
        // rule PerformancePolicy.InspectSpaceAsync applies one performance at
        // a time, checked once per distinct space instead of once per move.
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

        var moved = new List<(int Index, OrderSlot Slot)>(request.Moves.Count);

        for (var i = 0; i < request.Moves.Count; i++)
        {
            var move = request.Moves[i];
            var performance = performances[move.PerformanceId];

            moved.Add((i, new OrderSlot(
                performance.Id, string.Empty, move.VenueSpaceId, move.ScheduledOn, move.OrderNumber)));
        }

        // Team names are cosmetic — only for the message a conflict prints —
        // so they are read separately and only for the teams this batch
        // actually involves.
        var teamIds = performances.Values.Select(performance => performance.TeamId).ToHashSet();

        var teamNames = await database.Teams
            .AsNoTracking()
            .Where(team => teamIds.Contains(team.Id))
            .ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken);

        moved = [.. moved.Select(entry => (entry.Index, entry.Slot with
        {
            TeamName = teamNames.GetValueOrDefault(performances[entry.Slot.PerformanceId].TeamId, "?"),
        }))];

        // Every other live performance that could possibly collide: sharing
        // a mat the batch touches, with a slot already fully assigned.
        // Nothing else on the running order is relevant to whether this
        // particular batch fits.
        var others = await database.Performances
            .AsNoTracking()
            .Where(performance => !moveIds.Contains(performance.Id))
            .Where(performance => performance.VenueSpaceId != null
                && performance.ScheduledOn != null && performance.OrderNumber != null)
            .Where(performance => spaceIds.Contains(performance.VenueSpaceId!.Value))
            .Select(performance => new OrderSlot(
                performance.Id, performance.Team!.Name,
                performance.VenueSpaceId, performance.ScheduledOn, performance.OrderNumber))
            .ToListAsync(cancellationToken);

        var conflicts = PerformanceOrderConflicts.Find(moved, others);

        if (conflicts.Count > 0)
        {
            return Results.ValidationProblem(conflicts.ToDictionary(
                entry => $"Moves[{entry.Key}]", entry => entry.Value.ToArray()));
        }

        // Captured before anything is overwritten: whether a move actually
        // changes the slot, the only thing worth writing to a club about.
        var changedPerformanceIds = new List<Guid>();

        foreach (var move in request.Moves)
        {
            var performance = performances[move.PerformanceId];

            if (performance.VenueSpaceId != move.VenueSpaceId
                || performance.ScheduledOn != move.ScheduledOn
                || performance.OrderNumber != move.OrderNumber)
            {
                changedPerformanceIds.Add(performance.Id);
            }

            performance.VenueSpaceId = move.VenueSpaceId;
            performance.ScheduledOn = move.ScheduledOn;
            performance.OrderNumber = move.OrderNumber;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Only reachable if another request landed on one of these same
            // turns in the instant between the check above and this write —
            // the same race PerformancePolicy's own collision check cannot
            // close either, and uq_performance_running_order is what closes it.
            return Results.Problem(
                detail: "Algún turno se ocupó justo ahora, entre que se revisó y que se guardó. " +
                        "Volvé a intentar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (changedPerformanceIds.Count > 0)
        {
            Notify(context, jobs, organization.RequireOrganizationId(), organization.UserId ?? Guid.Empty, changedPerformanceIds);
        }

        return Results.Ok(new Response(request.Moves.Count));
    }

    /// <summary>
    /// Queued once the response has actually gone out — see
    /// <see cref="Matches.RescheduleMatch"/>'s own remarks on why. One job
    /// per performance rather than one for the whole batch, for the same
    /// reason as <see cref="Matches.RescheduleMatchesBulk"/>'s own Notify.
    /// </summary>
    private static void Notify(
        HttpContext context, IBackgroundJobClient jobs, Guid organizationId, Guid userId, List<Guid> performanceIds) =>
        context.Response.OnCompleted(() =>
        {
            foreach (var performanceId in performanceIds)
            {
                jobs.Enqueue<PerformanceRescheduleNotificationJob>(
                    job => job.RunAsync(organizationId, userId, performanceId, CancellationToken.None));
            }

            return Task.CompletedTask;
        });
}
