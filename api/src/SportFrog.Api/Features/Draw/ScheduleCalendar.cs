using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Puts the next jornada's undated fixtures on the calendar.
/// </summary>
/// <remarks>
/// One jornada per call — a single round of a single category — rather than
/// the whole competition at once. A multicategory competition is played in
/// blocks (every fixture of one division's round before the next division's
/// first), and that only means something if the organizer can also pick the
/// day each block lands on, or push one back without touching the rest. That
/// block order comes from <see cref="Category.DisplayOrder"/>, which already
/// exists for listing categories — the earliest category (by that order)
/// that still has an unplaced round is the one this call advances.
///
/// A format without rounds (a straight knockout, or a bracket phase after a
/// group stage) has no jornada to speak of — the next round of a bracket does
/// not exist until <c>AdvanceBracket</c> knows who won the last one, so it was
/// never a candidate for scheduling several rounds ahead in the first place.
/// This only ever looks at matches that carry a <see cref="Match.RoundNumber"/>.
/// </remarks>
public static class ScheduleCalendar
{
    /// <summary>
    /// A sensible gap between matches when nobody has said otherwise —
    /// enough to change ends and let the next team warm up, not so much that
    /// a full day of jornadas turns into mostly waiting.
    /// </summary>
    internal const short DefaultBufferMinutes = 15;

    /// <summary>
    /// Used only when a booking's own duration cannot be resolved at all —
    /// its reglamento has no clock and never declared an estimate either.
    /// Zero would silently stop that commitment from blocking anything,
    /// which is worse than holding the pitch a little longer than it
    /// probably needs.
    /// </summary>
    internal const short FallbackMinimumMinutes = 60;

    /// <param name="From">
    /// The first date to consider for this jornada. Left out, generation
    /// continues from the day of the competition's last scheduled match (or
    /// its own start date, or today) — so clicking again with nothing filled
    /// in just keeps going from wherever the last one landed.
    /// </param>
    /// <param name="StartTime">
    /// The earliest this jornada may start on <paramref name="From"/> itself,
    /// and every day after it this run needs (see <see cref="CalendarPlacement.Place"/>).
    /// Required rather than defaulted: which hour a pitch is actually free is
    /// a real-world arrangement the organizer settles before ever clicking
    /// "Generar siguiente jornada" (see <see cref="CalendarPlacement"/>'s own
    /// remarks), and a guessed default would be exactly the kind of
    /// scheduling assumption this software has no business making.
    /// </param>
    public sealed record Request(DateOnly? From, TimeOnly? StartTime);

    /// <param name="CategoryName">
    /// Which jornada this call advanced, so the organizer sees what just
    /// happened instead of a bare count. Null when nothing was pending at
    /// all.
    /// </param>
    /// <param name="Unplaced">
    /// Fixtures of this jornada that did not fit. Reported rather than
    /// hidden: more fixtures than hours is a real situation, and an organizer
    /// needs to know it before matchday rather than when a team asks where
    /// its match is.
    /// </param>
    /// <param name="HasMorePending">
    /// Whether calling this again would find another jornada — the signal
    /// for whether to keep offering the button.
    /// </param>
    public sealed record Response(
        string? CategoryName,
        short? RoundNumber,
        int Placed,
        int Unplaced,
        DateTimeOffset? FirstMatch,
        DateTimeOffset? LastMatch,
        bool HasMorePending);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.From)
                .GreaterThan(new DateOnly(2000, 1, 1))
                .When(request => request.From.HasValue)
                .WithMessage("Esa fecha de inicio no es una fecha posible.");

            RuleFor(request => request.StartTime)
                .NotNull()
                .WithMessage("La hora de inicio es obligatoria.");
        }
    }

    public static IEndpointRouteBuilder MapScheduleCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/schedule", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(ScheduleCalendar))
            .WithSummary("Places the next jornada's undated fixtures on the calendar.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        TimeProvider clock,
        OrganizationContext organization,
        IBackgroundJobClient jobs,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        if (competition.Settings.Schedule is not { SpaceIds.Count: > 0 } schedule)
        {
            return Results.Problem(
                detail: "Esta competencia no tiene canchas habilitadas. Elegí cuáles puede " +
                        "usar antes de colocar sus partidos.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var proxima = await FindNextJornadaAsync(database, competitionId, cancellationToken);

        if (proxima is null)
        {
            return Results.Ok(new Response(null, null, 0, 0, null, null, HasMorePending: false));
        }

        var pending = await database.Matches
            .Where(match => match.CategoryId == proxima.CategoryId
                && match.RoundNumber == proxima.RoundNumber
                && match.ScheduledAt == null
                && match.Status == MatchState.Scheduled)
            .ToListAsync(cancellationToken);

        // Every space this competition can use, and everything already
        // booked on any of them — whoever put it there, whichever
        // competition it belongs to. A space is shared by the whole
        // organization, so a fixture of another competition occupies it just
        // as firmly.
        var spaceIds = schedule.SpaceIds!;

        var takenMatches = await database.Matches
            .AsNoTracking()
            .Where(match => match.VenueSpaceId != null && match.ScheduledAt != null)
            .Where(match => spaceIds.Contains(match.VenueSpaceId!.Value))
            .Where(match => match.Status != MatchState.Cancelled
                && match.Status != MatchState.Postponed)
            .Select(match => new { match.CategoryId, match.VenueSpaceId, match.ScheduledAt })
            .ToListAsync(cancellationToken);

        // One resolution, shared by the jornada being placed and everything
        // already on those pitches: how long a match of a given category
        // actually takes, computed or declared from its own reglamento —
        // never from anything the competition itself sets.
        var durations = await ResolveDurationsAsync(
            database, [proxima.CategoryId, .. takenMatches.Select(match => match.CategoryId)], cancellationToken);

        if (durations[proxima.CategoryId] is not { } thisJornada)
        {
            return Results.Problem(
                detail: "El reglamento de esta categoría no dice cuánto ocupa un partido en un " +
                        "espacio. Completá su duración de período o, si el deporte no corre por " +
                        "reloj, su duración estimada, desde Reglamentos.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var taken = takenMatches.Select(match =>
        {
            var duration = durations[match.CategoryId] ?? new ResolvedDuration(FallbackMinimumMinutes, 0);
            var at = match.ScheduledAt!.Value;
            return new Booking(match.VenueSpaceId!.Value, at, at.AddMinutes(duration.Total));
        }).ToList();

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
            ?? LatestScheduledDay(engaged.Select(match => match.Day))
            ?? competition.StartsOn
            ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var placements = CalendarPlacement.Place(
            [.. pending.Select(match => new PendingFixture(match.Id, match.HomeTeamId, match.AwayTeamId))],
            thisJornada.GameMinutes,
            thisJornada.BufferMinutes,
            spaceIds,
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
            TimeSpan.Zero,
            // Guaranteed present — the Validator refuses a request without one.
            request.StartTime!.Value);

        var byId = pending.ToDictionary(match => match.Id);

        foreach (var placement in placements)
        {
            var match = byId[placement.MatchId];
            match.VenueSpaceId = placement.VenueSpaceId;
            match.ScheduledAt = placement.At;
        }

        await database.SaveChangesAsync(cancellationToken);

        if (placements.Count > 0)
        {
            var organizationId = organization.RequireOrganizationId();
            var userId = organization.UserId ?? Guid.Empty;

            // One job per fixture — see RescheduleMatch.Notify's own
            // remarks on why both the queuing point and the one-job-per-
            // fixture granularity matter. A jornada placed in one call can
            // be a dozen fixtures; a relay unreachable for one club's
            // notice must not hold up, or on retry resend, every other
            // club's.
            foreach (var placement in placements)
            {
                RescheduleMatch.Notify(context, jobs, organizationId, userId, placement.MatchId);
            }
        }

        // Covers both ways this call can leave work behind: fixtures of this
        // very jornada that did not fit, and any jornada after it — the
        // save above already persisted whatever did get placed, so a plain
        // re-check answers both at once.
        var hasMorePending = await database.Matches.AnyAsync(
            match => match.CompetitionId == competitionId
                && match.ScheduledAt == null
                && match.Status == MatchState.Scheduled
                && match.RoundNumber != null,
            cancellationToken);

        return Results.Ok(new Response(
            proxima.CategoryName,
            proxima.RoundNumber,
            placements.Count,
            pending.Count - placements.Count,
            placements.Count == 0 ? null : placements.Min(placement => placement.At),
            placements.Count == 0 ? null : placements.Max(placement => placement.At),
            hasMorePending));
    }

    /// <summary>Which jornada a call would advance, before anything about it is read further.</summary>
    internal sealed record NextJornada(Guid CategoryId, string CategoryName, short RoundNumber);

    /// <summary>
    /// The jornada this call is allowed to touch: the lowest unplaced round
    /// of the earliest category, by <see cref="Category.DisplayOrder"/>, that
    /// still owes one.
    /// </summary>
    /// <remarks>
    /// Read straight off Matches rather than through a Category.Matches
    /// collection, which does not exist: the category is a fact about the
    /// match, not the other way around. Two queries rather than one because
    /// the first only needs to know <em>which</em> category is next — the
    /// round is answered afterwards, scoped to that one category, so a
    /// category with several categories ahead of it in the queue is never
    /// asked to compute a MIN() over rounds nothing will use.
    /// </remarks>
    internal static async Task<NextJornada?> FindNextJornadaAsync(
        SportFrogDbContext database,
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        var category = await (
                from match in database.Matches
                join candidate in database.Categories on match.CategoryId equals candidate.Id
                where match.CompetitionId == competitionId
                    && match.ScheduledAt == null
                    && match.Status == MatchState.Scheduled
                    && match.RoundNumber != null
                orderby candidate.DisplayOrder, candidate.Name
                select new { candidate.Id, candidate.Name }
            )
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        var roundNumber = await database.Matches
            .Where(match => match.CategoryId == category.Id
                && match.ScheduledAt == null
                && match.Status == MatchState.Scheduled
                && match.RoundNumber != null)
            .MinAsync(match => match.RoundNumber!.Value, cancellationToken);

        return new NextJornada(category.Id, category.Name, roundNumber);
    }

    /// <summary>
    /// The day generation should resume from when the caller did not name
    /// one — the day of whatever is already the latest scheduled fixture in
    /// the competition, so a click with no date filled in continues right
    /// after the previous jornada instead of restarting from today.
    /// </summary>
    private static DateOnly? LatestScheduledDay(IEnumerable<DateOnly> scheduledDays)
    {
        using var days = scheduledDays.GetEnumerator();
        if (!days.MoveNext())
        {
            return null;
        }

        var latest = days.Current;
        while (days.MoveNext())
        {
            if (days.Current > latest)
            {
                latest = days.Current;
            }
        }

        return latest;
    }

    /// <summary>How long a match of one category takes, split so a caller can add its own buffer or not.</summary>
    internal readonly record struct ResolvedDuration(short GameMinutes, short BufferMinutes)
    {
        public short Total => (short)(GameMinutes + BufferMinutes);
    }

    /// <summary>
    /// Resolves, for each category asked about, how long one of its matches
    /// takes — entirely from its own reglamento (see
    /// <see cref="MatchDuration.From"/>, which itself computes a clocked
    /// sport's duration or reads a clockless one's declared estimate), plus
    /// whatever buffer its own competition leaves between matches. Shared
    /// between the jornada being placed and every pitch already booked, which
    /// can belong to other categories or other competitions entirely.
    /// </summary>
    internal static async Task<Dictionary<Guid, ResolvedDuration?>> ResolveDurationsAsync(
        SportFrogDbContext database,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        // Settings is a single jsonb column behind a value converter, not a
        // set of columns EF can decompose in SQL — the whole thing has to
        // come back and be read in memory, so .Schedule is a plain property
        // access below rather than part of the projection itself.
        var categories = await database.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new
            {
                category.Id,
                EffectiveRulesetId = category.RulesetId ?? category.Competition!.RulesetId,
                category.Competition!.Settings,
            })
            .ToListAsync(cancellationToken);

        var rulesetIds = categories.Select(category => category.EffectiveRulesetId).Distinct().ToList();
        var configs = await database.Rulesets
            .AsNoTracking()
            .Where(ruleset => rulesetIds.Contains(ruleset.Id))
            .ToDictionaryAsync(ruleset => ruleset.Id, ruleset => ruleset.Config, cancellationToken);

        var resolved = new Dictionary<Guid, ResolvedDuration?>();

        foreach (var category in categories)
        {
            var game = configs.TryGetValue(category.EffectiveRulesetId, out var config)
                ? MatchDuration.From(config.Periods)
                : null;

            resolved[category.Id] = game is { } minutes
                ? new ResolvedDuration(minutes, category.Settings.Schedule?.BufferMinutes ?? DefaultBufferMinutes)
                : null;
        }

        return resolved;
    }
}
