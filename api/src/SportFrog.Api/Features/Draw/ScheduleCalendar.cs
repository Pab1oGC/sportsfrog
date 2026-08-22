using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Puts the undated fixtures of a competition on the calendar.
/// </summary>
/// <remarks>
/// Per competition rather than per category, and that is the whole reason it
/// is useful: a Sunday has fixtures of four divisions on three pitches, and a
/// placement that could only see one division at a time would hand the same
/// slot out twice. The database would refuse the second — two matches cannot
/// share a pitch — but only after the organizer had been told it worked.
/// </remarks>
public static class ScheduleCalendar
{
    /// <param name="From">
    /// The first date to consider. Left out, the competition's own start date
    /// is used, and failing that today.
    /// </param>
    public sealed record Request(DateOnly? From);

    /// <param name="Unplaced">
    /// Fixtures that did not fit. Reported rather than hidden: more fixtures
    /// than hours is a real situation, and an organizer needs to know it
    /// before the season starts rather than when a team asks where its match
    /// is.
    /// </param>
    public sealed record Response(
        int Placed,
        int Unplaced,
        DateTimeOffset? FirstMatch,
        DateTimeOffset? LastMatch);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.From)
                .GreaterThan(new DateOnly(2000, 1, 1))
                .When(request => request.From.HasValue)
                .WithMessage("That start date is not a plausible one.");
    }

    public static IEndpointRouteBuilder MapScheduleCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/schedule", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(ScheduleCalendar))
            .WithSummary("Places the undated fixtures of a competition on the calendar.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        if (competition.Settings.Schedule is not { Spaces.Count: > 0 } schedule)
        {
            return Results.Problem(
                detail: "This competition has no scheduling windows. Set which spaces it can use, " +
                        "on which days and between which hours, before placing its fixtures.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (schedule.SlotMinutes <= 0)
        {
            return Results.Problem(
                detail: "The scheduling windows do not say how long a fixture occupies a space.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Only fixtures still waiting for a date, and only ones that will be
        // played. A postponed match owes a result and is placed again by
        // whoever reschedules it; a cancelled one owes nothing.
        var pending = await database.Matches
            .Where(match => match.CompetitionId == competitionId)
            .Where(match => match.ScheduledAt == null)
            .Where(match => match.Status == MatchState.Scheduled)
            .OrderBy(match => match.RoundNumber)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return Results.Ok(new Response(0, 0, null, null));
        }

        // Everything already on those pitches, whoever put it there and
        // whichever competition it belongs to. A space is shared by the whole
        // organization, so a fixture of another competition occupies it just
        // as firmly.
        var spaceIds = schedule.Spaces!.Select(space => space.VenueSpaceId).ToList();

        var taken = await database.Matches
            .AsNoTracking()
            .Where(match => match.VenueSpaceId != null && match.ScheduledAt != null)
            .Where(match => spaceIds.Contains(match.VenueSpaceId!.Value))
            .Where(match => match.Status != MatchState.Cancelled
                && match.Status != MatchState.Postponed)
            .Select(match => new Booking(match.VenueSpaceId!.Value, match.ScheduledAt!.Value))
            .ToListAsync(cancellationToken);

        // And the teams those fixtures commit, day by day. A team is booked
        // by its match as surely as the pitch is, so a fixture set by hand on
        // Saturday morning has to stop the placement putting the same team on
        // Saturday afternoon. Read across the whole competition rather than
        // only the spaces above: a team committed at a ground this schedule
        // does not mention is still committed.
        var engaged = await database.Matches
            .AsNoTracking()
            .Where(match => match.CompetitionId == competitionId && match.ScheduledAt != null)
            .Where(match => match.Status != MatchState.Cancelled)
            .Select(match => new
            {
                match.HomeTeamId,
                match.AwayTeamId,
                Day = DateOnly.FromDateTime(match.ScheduledAt!.Value.UtcDateTime),
            })
            .ToListAsync(cancellationToken);

        var from = request.From
            ?? competition.StartsOn
            ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var placements = CalendarPlacement.Place(
            [.. pending.Select(match => new PendingFixture(
                match.Id, match.RoundNumber ?? 0, match.HomeTeamId, match.AwayTeamId))],
            schedule,
            taken,
            [
                .. engaged.SelectMany(match => new[]
                {
                    (Team: match.HomeTeamId, match.Day),
                    (Team: match.AwayTeamId, match.Day),
                }),
            ],
            from,

            // Stored with an offset because the column is timestamptz. UTC is
            // the honest one to write: the organization's own zone is not
            // recorded anywhere, and inventing one here would put every
            // fixture an hour out for half the year.
            TimeSpan.Zero);

        var byId = pending.ToDictionary(match => match.Id);

        foreach (var placement in placements)
        {
            var match = byId[placement.MatchId];
            match.VenueSpaceId = placement.VenueSpaceId;
            match.ScheduledAt = placement.At;
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            placements.Count,
            pending.Count - placements.Count,
            placements.Count == 0 ? null : placements.Min(placement => placement.At),
            placements.Count == 0 ? null : placements.Max(placement => placement.At)));
    }
}
