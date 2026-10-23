using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Features.Rulebook;
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
    /// in just keeps going from wherever the last one landed. Either way,
    /// combined with <paramref name="StartTime"/> it has to land on or after
    /// right now, and on or after this category's own last dated fixture —
    /// see <see cref="JornadaWindow"/>.
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
    /// <param name="UtcOffsetMinutes">
    /// How far the organizer's clock is from UTC, in minutes (UTC-4 is -240).
    /// <paramref name="StartTime"/> is a wall-clock hour with no zone of its
    /// own, and the column it lands in is a point in time — without this the
    /// hour typed is read as UTC and shows up hours early on the organizer's
    /// own screen. Left out, UTC, which is what this used to assume.
    /// </param>
    public sealed record Request(DateOnly? From, TimeOnly? StartTime, int? UtcOffsetMinutes = null);

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

            // UTC-12 to UTC+14 are the zones that exist; anything past that
            // is a slipped value, and DateTimeOffset would throw on it.
            RuleFor(request => request.UtcOffsetMinutes)
                .InclusiveBetween(-14 * 60, 14 * 60)
                .When(request => request.UtcOffsetMinutes.HasValue)
                .WithMessage("Ese desfase horario no es posible.");
        }
    }

    public static IEndpointRouteBuilder MapScheduleCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/schedule", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ScheduleCalendar))
            .WithSummary("Places the next jornada's undated fixtures on the calendar.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        TimeProvider clock,
        OrganizationContext organization,
        IBackgroundJobClient jobs,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions
            .Include(candidate => candidate.Sport)
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        if (competition?.Sport is not { } sport)
        {
            return Results.NotFound();
        }

        // The organizer's clock, for everything below that turns a wall-clock
        // hour into an instant or an instant back into "which day is this".
        var offset = TimeSpan.FromMinutes(request.UtcOffsetMinutes ?? 0);

        var proxima = await FindNextJornadaAsync(database, competitionId, cancellationToken);

        if (proxima is null)
        {
            return Results.Ok(new Response(null, null, 0, 0, null, null, HasMorePending: false));
        }

        var pending = await database.Matches
            .Where(match => match.CategoryId == proxima.CategoryId
                && match.RoundNumber == proxima.RoundNumber
                && match.ScheduledAt == null
                && match.Status == MatchState.Scheduled
                // Same reason as FindNextJornadaAsync's own filter: one round
                // of a knockout can mix a match both byes already settled
                // with one still waiting on another match's winner, and only
                // the settled one is this auto-placer's business.
                && match.HomeTeamId != null && match.AwayTeamId != null)
            .ToListAsync(cancellationToken);

        // Every match of one round shares one Phase (the draw assigns it per
        // round, never per match), so any one of them says which of this
        // category's own stages this jornada belongs to — see
        // CompetitionStages.KindOfMatch, the same rule MatchRulesLookup and
        // EventPolicy already resolve a single fixture's stage by.
        var thisStage = CompetitionStages.KindOfMatch(
            competition.Format, sport.ScoreMode, pending.FirstOrDefault()?.Phase);

        var stageSchedule = await database.CompetitionStages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CompetitionId == competitionId && candidate.Kind == thisStage,
                cancellationToken);

        if (stageSchedule is not { SpaceIds.Length: > 0 })
        {
            return Results.Problem(
                detail: "Esta fase no tiene canchas habilitadas. Elegí cuáles puede usar antes " +
                        "de colocar sus partidos.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Every space this stage can use, and everything already booked on
        // any of them — whoever put it there, whichever competition or
        // stage it belongs to. A space is shared by the whole organization,
        // so a fixture of another competition occupies it just as firmly.
        var spaceIds = stageSchedule.SpaceIds!;

        var takenMatches = await database.Matches
            .AsNoTracking()
            .Where(match => match.VenueSpaceId != null && match.ScheduledAt != null)
            .Where(match => spaceIds.Contains(match.VenueSpaceId!.Value))
            .Where(match => match.Status != MatchState.Cancelled
                && match.Status != MatchState.Postponed)
            .Select(match => new
            {
                match.CategoryId,
                match.Competition!.Format,
                match.Competition.Sport!.ScoreMode,
                match.Phase,
                match.VenueSpaceId,
                match.ScheduledAt,
            })
            .ToListAsync(cancellationToken);

        // Each taken fixture's own stage, the same derivation as this
        // jornada's — a pitch already booked by another category's bracket
        // match takes however long *that* stage's reglamento says, not this
        // jornada's.
        var takenStages = takenMatches
            .Select(match => (
                match.CategoryId,
                Kind: CompetitionStages.KindOfMatch(match.Format, match.ScoreMode, match.Phase)))
            .ToList();

        // One resolution, shared by the jornada being placed and everything
        // already on those pitches: how long a match of a given category's
        // stage actually takes, computed or declared from its own
        // reglamento — never from anything the competition itself sets.
        var durations = await ResolveDurationsAsync(
            database, [(proxima.CategoryId, thisStage), .. takenStages], cancellationToken);

        if (durations[(proxima.CategoryId, thisStage)] is not { } thisJornada)
        {
            return Results.Problem(
                detail: "El reglamento de esta categoría no dice cuánto ocupa un partido en un " +
                        "espacio. Completá su duración de período o, si el deporte no corre por " +
                        "reloj, su duración estimada, desde Reglamentos.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var taken = takenMatches.Zip(takenStages, (match, stage) =>
        {
            var duration = durations[(stage.CategoryId, stage.Kind)] ?? new ResolvedDuration(FallbackMinimumMinutes, 0);
            var at = match.ScheduledAt!.Value;
            return new Booking(match.VenueSpaceId!.Value, at, at.AddMinutes(duration.Total));
        }).ToList();

        // And the teams those fixtures commit, day by day. A team is booked
        // by its match as surely as the pitch is, so a fixture set by hand on
        // Saturday morning has to stop the placement putting the same team on
        // Saturday afternoon. Read across the whole competition rather than
        // only the spaces above: a team committed at a ground this schedule
        // does not mention is still committed.
        //
        // The day is the organizer's, not UTC's: at UTC-4 a match at 21:00 is
        // already tomorrow in UTC, and reading it that way would let its team
        // be placed again on the evening it is really playing.
        var engaged = (await database.Matches
                .AsNoTracking()
                .Where(match => match.CompetitionId == competitionId && match.ScheduledAt != null)
                .Where(match => match.Status != MatchState.Cancelled)
                .Select(match => new { match.HomeTeamId, match.AwayTeamId, match.ScheduledAt })
                .ToListAsync(cancellationToken))
            .Select(match => new
            {
                match.HomeTeamId,
                match.AwayTeamId,
                Day = DateOnly.FromDateTime(match.ScheduledAt!.Value.ToOffset(offset).DateTime),
            })
            .ToList();

        var from = request.From
            ?? LatestScheduledDay(engaged.Select(match => match.Day))
            ?? competition.StartsOn
            ?? DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(offset).DateTime);

        // The same point in time CalendarPlacement.Place is about to search
        // forward from, checked once before it runs rather than after it has
        // already placed something. Applies whether that date came from the
        // request or from the fallback above: a category whose last jornada
        // was played weeks ago defaults "from" to that same day, which is
        // exactly as behind-the-clock as if the organizer had typed it.
        var requestedStart = new DateTimeOffset(from.ToDateTime(request.StartTime!.Value), offset);

        var latestScheduled = await LatestScheduledInstantAsync(database, proxima.CategoryId, cancellationToken);

        var violation = JornadaWindow.Inspect(requestedStart, clock.GetUtcNow(), latestScheduled);

        if (violation != JornadaWindow.Violation.None)
        {
            return Results.Problem(
                detail: violation == JornadaWindow.Violation.InThePast
                    ? "No se puede generar una jornada en el pasado. Elegí una fecha y hora que todavía no pasaron."
                    : "Esta categoría ya tiene partidos programados después de esa fecha. La próxima " +
                      "jornada tiene que ir después de la última ya programada.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var placements = CalendarPlacement.Place(
            // The filter above already refused any of these a null side.
            [.. pending.Select(match => new PendingFixture(match.Id, match.HomeTeamId!.Value, match.AwayTeamId!.Value))],
            thisJornada.GameMinutes,
            thisJornada.BufferMinutes,
            spaceIds,
            taken,
            [
                .. engaged
                    .Where(match => match.HomeTeamId != null && match.AwayTeamId != null)
                    .SelectMany(match => new[]
                    {
                        (Team: match.HomeTeamId!.Value, match.Day),
                        (Team: match.AwayTeamId!.Value, match.Day),
                    }),
            ],
            from,

            // The organizer's own offset, as the browser reported it for the
            // day being placed. The organization's zone is not recorded
            // anywhere, so it is asked of the one clock that knows it —
            // writing the typed hour as UTC put every fixture hours out on
            // the screen it was typed on.
            offset,
            // Guaranteed present — the Validator refuses a request without one.
            request.StartTime!.Value);

        var byId = pending.ToDictionary(match => match.Id);

        foreach (var placement in placements)
        {
            var match = byId[placement.MatchId];
            match.VenueSpaceId = placement.VenueSpaceId;

            // Npgsql writes timestamptz only from an offset-zero value and
            // throws on anything else. Same instant, so the hour typed in the
            // organizer's zone is what comes back out of it.
            match.ScheduledAt = placement.At.ToUniversalTime();
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // One of these pending fixtures was itself changed (rescheduled,
            // given a result) by somebody else since it was read as
            // "pending" moments ago.
            return Results.Problem(
                detail: "Alguno de estos partidos cambió justo ahora. Volvé a pedir el próximo " +
                        "grupo de fechas antes de programarlas.",
                statusCode: StatusCodes.Status409Conflict);
        }

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
                    // A knockout drawn in full can have a round whose teams
                    // are still "whoever wins another match" — nothing this
                    // auto-placer could space out by team, since there is no
                    // team yet to check a day against. Left for
                    // ScheduleMatch/RescheduleMatch, one fixture at a time,
                    // once it is worth booking a pitch for.
                    && match.HomeTeamId != null && match.AwayTeamId != null
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
                && match.RoundNumber != null
                && match.HomeTeamId != null && match.AwayTeamId != null)
            .MinAsync(match => match.RoundNumber!.Value, cancellationToken);

        return new NextJornada(category.Id, category.Name, roundNumber);
    }

    /// <summary>
    /// When this category's own last dated fixture is, if it has ever had
    /// one.
    /// </summary>
    /// <remarks>
    /// Scoped to the one category a call is about to advance, not the whole
    /// competition: categories play in blocks (see the class remarks), so
    /// one category's jornada has nothing to say about where another's
    /// calendar already stands. Cancelled and postponed fixtures are left
    /// out — neither holds its slot the way a live one does, the same
    /// exclusion <see cref="SportFrog.Api.Features.Matches.FixturePolicy"/>'s
    /// own collision checks already apply — so neither should hold the line
    /// for <see cref="JornadaWindow"/> either.
    /// </remarks>
    internal static Task<DateTimeOffset?> LatestScheduledInstantAsync(
        SportFrogDbContext database,
        Guid categoryId,
        CancellationToken cancellationToken) =>
        database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId && match.ScheduledAt != null)
            .Where(match => match.Status != MatchState.Cancelled && match.Status != MatchState.Postponed)
            .Select(match => (DateTimeOffset?)match.ScheduledAt)
            .MaxAsync(cancellationToken);

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
    /// Resolves, for each (category, stage) pair asked about, how long one
    /// of its matches takes — entirely from its own reglamento (see
    /// <see cref="MatchDuration.From"/>, which itself computes a clocked
    /// sport's duration or reads a clockless one's declared estimate), with
    /// the stage's own override ahead of the category's and the
    /// competition's (see <see cref="EffectiveRuleset"/>), plus whatever
    /// buffer that stage leaves between matches. Shared between the jornada
    /// being placed and every pitch already booked, which can belong to
    /// other categories, other stages, or other competitions entirely.
    /// </summary>
    internal static async Task<Dictionary<(Guid CategoryId, StageKind Kind), ResolvedDuration?>> ResolveDurationsAsync(
        SportFrogDbContext database,
        IReadOnlyCollection<(Guid CategoryId, StageKind Kind)> categoryStages,
        CancellationToken cancellationToken)
    {
        var categoryIds = categoryStages.Select(pair => pair.CategoryId).Distinct().ToList();

        var categories = await database.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new
            {
                category.Id,
                category.CompetitionId,
                category.RulesetId,
                CompetitionRulesetId = category.Competition!.RulesetId,
            })
            .ToDictionaryAsync(category => category.Id, cancellationToken);

        var competitionIds = categories.Values.Select(category => category.CompetitionId).Distinct().ToList();

        // A stage's own reglamento override, where one of these categories
        // declared one — see EffectiveRuleset's third level.
        var categoryStageRulesets = await database.CategoryStages
            .AsNoTracking()
            .Where(stage => categoryIds.Contains(stage.CategoryId))
            .Select(stage => new { stage.CategoryId, stage.Kind, stage.RulesetId })
            .ToListAsync(cancellationToken);

        // A stage's own margin between matches — see CompetitionStage.BufferMinutes.
        var competitionStageBuffers = await database.CompetitionStages
            .AsNoTracking()
            .Where(stage => competitionIds.Contains(stage.CompetitionId))
            .Select(stage => new { stage.CompetitionId, stage.Kind, stage.BufferMinutes })
            .ToListAsync(cancellationToken);

        var effectiveRulesetIds = categoryStages
            .Select(pair => EffectiveRuleset.Of(
                categoryStageRulesets.Find(stage => stage.CategoryId == pair.CategoryId && stage.Kind == pair.Kind)?.RulesetId,
                categories[pair.CategoryId].RulesetId,
                categories[pair.CategoryId].CompetitionRulesetId))
            .Distinct()
            .ToList();

        var configs = await database.Rulesets
            .AsNoTracking()
            .Where(ruleset => effectiveRulesetIds.Contains(ruleset.Id))
            .ToDictionaryAsync(ruleset => ruleset.Id, ruleset => ruleset.Config, cancellationToken);

        var resolved = new Dictionary<(Guid CategoryId, StageKind Kind), ResolvedDuration?>();

        foreach (var pair in categoryStages)
        {
            var category = categories[pair.CategoryId];

            var effectiveRulesetId = EffectiveRuleset.Of(
                categoryStageRulesets.Find(stage => stage.CategoryId == pair.CategoryId && stage.Kind == pair.Kind)?.RulesetId,
                category.RulesetId,
                category.CompetitionRulesetId);

            var game = configs.TryGetValue(effectiveRulesetId, out var config)
                ? MatchDuration.From(config.Periods)
                : null;

            var bufferMinutes = competitionStageBuffers
                .Find(stage => stage.CompetitionId == category.CompetitionId && stage.Kind == pair.Kind)
                ?.BufferMinutes ?? DefaultBufferMinutes;

            resolved[pair] = game is { } minutes ? new ResolvedDuration(minutes, bufferMinutes) : null;
        }

        return resolved;
    }
}
