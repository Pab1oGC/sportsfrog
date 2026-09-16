using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Performances;

/// <summary>Something a performance's running-order slot says that nothing else allows.</summary>
internal sealed record PerformanceSlotViolation(string Property, string Message);

/// <summary>
/// Whether a team's turn in a classification stage's running order can be
/// set: the mat has to exist and be usable, and nothing else already holds
/// that exact mat, day and turn.
/// </summary>
/// <remarks>
/// Deliberately does not ask whether the team is already busy elsewhere at
/// that "turn", unlike <see cref="Matches.FixturePolicy"/>'s exact-instant
/// check for a fixture. A running-order position on one mat and the same
/// position on another mat do not happen at the same real moment — each mat
/// runs through its own queue at its own pace — so there is nothing honest
/// to compare a team's other turns against. See <see cref="Performances.Performance.OrderNumber"/>.
/// </remarks>
internal sealed class PerformancePolicy(SportFrogDbContext database)
{
    /// <param name="excludingPerformanceId">
    /// The performance being corrected, left out of its own collision check —
    /// otherwise a performance would always collide with itself the moment
    /// <see cref="SchedulePerformance"/> re-inspects it unchanged.
    /// </param>
    public async Task<IReadOnlyList<PerformanceSlotViolation>> InspectAsync(
        Guid? venueSpaceId,
        DateOnly? scheduledOn,
        short? orderNumber,
        Guid excludingPerformanceId,
        CancellationToken cancellationToken)
    {
        var violations = new List<PerformanceSlotViolation>();

        await InspectSpaceAsync(venueSpaceId, violations, cancellationToken);

        if (venueSpaceId is not null && scheduledOn is not null && orderNumber is not null)
        {
            await InspectCollisionAsync(
                venueSpaceId.Value, scheduledOn.Value, orderNumber.Value,
                excludingPerformanceId, violations, cancellationToken);
        }

        return violations;
    }

    /// <summary>The mat has to exist and be usable. Same rule as <c>FixturePolicy.InspectSpaceAsync</c>.</summary>
    private async Task InspectSpaceAsync(
        Guid? venueSpaceId,
        List<PerformanceSlotViolation> violations,
        CancellationToken cancellationToken)
    {
        if (venueSpaceId is not { } spaceId)
        {
            return;
        }

        var space = await database.VenueSpaces
            .AsNoTracking()
            .Where(candidate => candidate.Id == spaceId)
            .Select(candidate => new
            {
                candidate.Name,
                VenueName = candidate.Venue!.Name,
                Available = candidate.IsActive && candidate.Venue.IsActive,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (space is null)
        {
            violations.Add(new PerformanceSlotViolation(
                "VenueSpaceId", "Ningún espacio de esta organización tiene ese identificador."));
            return;
        }

        if (!space.Available)
        {
            violations.Add(new PerformanceSlotViolation(
                "VenueSpaceId",
                $"{space.VenueName} / {space.Name} no está disponible, así que no se puede " +
                "programar nada ahí."));
        }
    }

    /// <summary>
    /// One team per mat, per day, per turn. The schema has the last word —
    /// <c>uq_performance_running_order</c> refuses the same three twice —
    /// this is what turns that constraint's generic rejection into a
    /// sentence naming who already has the turn, for the ordinary case where
    /// two requests are not racing each other.
    /// </summary>
    private async Task InspectCollisionAsync(
        Guid venueSpaceId,
        DateOnly scheduledOn,
        short orderNumber,
        Guid excludingPerformanceId,
        List<PerformanceSlotViolation> violations,
        CancellationToken cancellationToken)
    {
        var holder = await database.Performances
            .AsNoTracking()
            .Where(candidate => candidate.Id != excludingPerformanceId)
            .Where(candidate => candidate.VenueSpaceId == venueSpaceId
                && candidate.ScheduledOn == scheduledOn && candidate.OrderNumber == orderNumber)
            .Select(candidate => candidate.Team!.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (holder is not null)
        {
            violations.Add(new PerformanceSlotViolation(
                "OrderNumber", $"Ese turno en ese espacio ya lo tiene {holder}."));
        }
    }
}
