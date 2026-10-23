using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Records what a match finished, or corrects what was recorded.
/// </summary>
/// <remarks>
/// The score is what makes a match finished, which is why this is not a
/// status change with a body attached. <c>ck_result_completeness</c> refuses a
/// finished match without totals and an author, so the two arrive together or
/// not at all.
///
/// Only the periods are sent. The match score is computed from them according
/// to the sport, because a caller that could state both could state a football
/// match of 1-0 and 2-1 that finished 5-0, and nothing would notice.
///
/// Two ways to arrive at those periods share this file: typed in by hand, or
/// tallied from the events a recorder was already entering while the match
/// was played. A team that logs every goal as it happens should not have to
/// count them again from a blank form the moment the match is over.
/// </remarks>
public static class RecordResult
{
    public sealed record Request(IReadOnlyList<PeriodScore> PeriodScores, string? Notes);

    /// <summary>
    /// Somebody sent off in this match, nobody has sanctioned yet for it.
    /// </summary>
    /// <remarks>
    /// A suggestion, never an action: what a red card is worth — how many
    /// matches, whether a fine too — is the organizer's own rule, the same
    /// reason the competition bulletin states it as prose rather than this
    /// schema modeling it (see <see cref="SportFrog.Domain.Discipline.SanctionCompletionRule"/>'s
    /// own remarks). Recording a result only ever surfaces that a red card
    /// was shown; imposing the sanction it might call for is a separate,
    /// deliberate act through <c>ImposeSanction</c>.
    /// </remarks>
    public sealed record SuggestedSanction(
        Guid AthleteId, string AthleteFirstName, string AthleteLastName, Guid TeamId, string TeamName, Guid MatchId);

    /// <summary>
    /// Somebody on either side's roster who carries a sanction that blocks
    /// them today.
    /// </summary>
    /// <remarks>
    /// A warning, not a refusal: whether this person actually took the field
    /// is not something recording a result can see — the schema has no
    /// lineup, only what was logged against the match (see
    /// <see cref="Infrastructure.Persistence.Entities.PlayerEvent"/>'s own
    /// remarks) — so a quiet substitute who never touched the ball would be
    /// invisible to this the same way a quiet starter is. This names
    /// everyone eligible for the roster who is sanctioned, for whoever
    /// recorded the result to check by eye, rather than silently trusting
    /// that nobody suspended was fielded.
    /// </remarks>
    public sealed record SuspendedPlayerWarning(
        Guid AthleteId, string AthleteFirstName, string AthleteLastName, Guid TeamId, string TeamName, string SanctionReason);

    /// <param name="HomeTotal">
    /// The match score, consolidated. Returned because it is derived rather
    /// than sent, and the caller has no other way to learn what their periods
    /// added up to.
    /// </param>
    public sealed record Response(
        int HomeTotal,
        int AwayTotal,
        IReadOnlyList<SuggestedSanction> SuggestedSanctions,
        IReadOnlyList<SuspendedPlayerWarning> SuspendedPlayerWarnings);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.PeriodScores)
                .NotEmpty().WithMessage("El marcador de cada período es obligatorio.");

            RuleFor(request => request.Notes)
                .MaximumLength(1000)
                .When(request => request.Notes is not null);
        }
    }

    public static IEndpointRouteBuilder MapRecordResult(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/matches/{id:guid}/result", HandleAsync)
            // The role exists for this. Recording what happened on the field
            // is the narrowest job in the organization, and it should not
            // require the authority to redraw the calendar.
            .RequireRole(MembershipRole.Recorder)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Match, "id"))
            .WithName(nameof(RecordResult))
            .WithSummary("Records the result of a match.");

        routes.MapPost("/matches/{id:guid}/result/from-events", HandleFromEventsAsync)
            .RequireRole(MembershipRole.Recorder)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Match, "id"))
            .WithName("RecordResultFromEvents")
            .WithSummary("Finishes a match with the score tallied from its recorded events.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        MatchRulesLookup rulesLookup,
        ResultPolicy policy,
        IMatchOutcomeRulesRegistry outcomeRules,
        ActiveSanctions activeSanctions,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (match, rules, refusal) = await LoadAsync(id, database, rulesLookup, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var violations = policy.Inspect(rules!, request.PeriodScores);

        if (violations.Count > 0)
        {
            return Refuse(violations);
        }

        if (RefuseUnpriceableTie(match!, rules!, request.PeriodScores) is { } unpriceable)
        {
            return unpriceable;
        }

        if (await RefuseIfContradictsEventsAsync(match!, rules!, request.PeriodScores, database, cancellationToken)
            is { } contradiction)
        {
            return contradiction;
        }

        return await FinishAsync(
            match!, rules!, request.PeriodScores, request.Notes, database, outcomeRules, activeSanctions,
            organization, clock, cancellationToken);
    }

    /// <summary>
    /// Whether a tied cumulative result is one this competition can
    /// actually do anything with.
    /// </summary>
    /// <remarks>
    /// A league match is allowed to end level — <see cref="MatchOutcomes.Draw"/>
    /// is a real outcome, priced however <see cref="RulesetConfiguration.Points"/>
    /// says to. Nothing before this checked, at the moment a result is
    /// recorded, whether a particular tie is one the ruleset actually prices
    /// or one a shootout can still resolve — a knockout match
    /// (<see cref="Match.Phase"/> is not null) always has
    /// <see cref="RecordPenalties"/> waiting for it afterwards, the same
    /// draw <see cref="SportFrog.Api.Features.Draw.AdvanceBracket"/> already
    /// refuses to carry a next round out of. A league match that ties
    /// without the ruleset pricing a draw has neither: <c>StandingsCalculator</c>
    /// silently credits it zero points for both sides, with nothing anywhere
    /// telling anyone why the table does not add up.
    ///
    /// Only <see cref="ScoreMode.Cumulative"/> ever reaches this with
    /// anything to check: <see cref="SetsResultShape"/> already refuses any
    /// tied period on its own, and a sets-mode match's own outcome
    /// (<c>win_3_1</c>, never a bare "draw") is exactly the set of keys
    /// <see cref="Rulebook.RulesetPolicy"/> already required the ruleset to
    /// price before it could be saved.
    /// </remarks>
    internal static IResult? RefuseUnpriceableTie(
        Match match, MatchRules rules, IReadOnlyList<PeriodScore> periods)
    {
        if (rules.Sport.ScoreMode != ScoreMode.Cumulative
            || match.Phase is not null
            || rules.Configuration.Points.ContainsKey(MatchOutcomes.Draw)
            || periods.Sum(period => period.Home) != periods.Sum(period => period.Away))
        {
            return null;
        }

        return Results.Problem(
            detail: "Este reglamento no admite empates, y este partido no es de una eliminatoria " +
                    "que lo pueda desempatar por penales. Si se jugó una prórroga, cargá el " +
                    "resultado con ella incluida; si no, el reglamento necesita admitir el empate.",
            statusCode: StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Whether a result typed in by hand says something this match's own
    /// events already disagree with.
    /// </summary>
    /// <remarks>
    /// Only once there is something to disagree with. A sport with no
    /// scoring metric at all (wally), or a match nobody logged a single
    /// qualifying event for — the recorder who skips live entry and types
    /// the final score straight in, exactly the case
    /// <see cref="HandleFromEventsAsync"/>'s own remarks describe as the
    /// only way in for those — has nothing here for the typed score to
    /// contradict, and is let through unchecked, same as before this
    /// existed.
    ///
    /// Once events exist, though, letting them and the typed score each say
    /// something different is the exact inconsistency this whole feature
    /// closes: a 2-1 half nobody's goals add up to is not a different
    /// opinion about the match, it is one of the two numbers being wrong.
    /// </remarks>
    private static async Task<IResult?> RefuseIfContradictsEventsAsync(
        Match match,
        MatchRules rules,
        IReadOnlyList<PeriodScore> submitted,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var (fromEvents, hasEvents, refusal) =
            await TryDerivePeriodsFromEventsAsync(match, rules, database, cancellationToken);

        if (refusal is not null || !hasEvents || fromEvents is null)
        {
            return null;
        }

        var byPeriod = submitted.ToDictionary(period => period.Period);

        foreach (var derived in fromEvents)
        {
            if (byPeriod.TryGetValue(derived.Period, out var typed)
                && typed.Home == derived.Home && typed.Away == derived.Away)
            {
                continue;
            }

            var typedLabel = byPeriod.TryGetValue(derived.Period, out var mismatched)
                ? $"{mismatched.Home}-{mismatched.Away}"
                : "sin cargar";

            return Results.Problem(
                detail: $"El {rules.Configuration.Periods.Label} {derived.Period} que cargaste a mano " +
                        $"({typedLabel}) no coincide con lo que dicen los eventos de este partido " +
                        $"({derived.Home}-{derived.Away}). Corregí los eventos o el marcador, pero no " +
                        "pueden contradecirse.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return null;
    }

    /// <summary>
    /// Finishes a match without asking anyone to type a score: the events
    /// already logged against it — every goal, or every taekwondo point and
    /// gam-jeom, tagged with the period it happened in — are exactly what a
    /// period-by-period result says, added up instead of remembered.
    /// </summary>
    /// <remarks>
    /// Refused only when this sport's ruleset has no metric that actually
    /// moves the score — a wally point is a statistic, not something to
    /// sum, so there is nothing here to tally and the manual form is the
    /// only way in. That used to be every sport decided in sets, but it no
    /// longer is: a taekwondo kyorugi point or gam-jeom, and a volleyball
    /// point, ace, block_point or error, all do affect the score (see
    /// <c>AddTaekwondoKyorugiScoringEvents</c> and
    /// <c>AddVolleyballScoringMetrics</c>), so this now asks the catalog
    /// directly instead of assuming the answer from the score mode.
    ///
    /// A sets-mode sport that does qualify still is not a cumulative one:
    /// <see cref="LiveScore.ComputePlayedPeriods"/> reports only the asaltos
    /// something was actually recorded for, not every one the ruleset
    /// configures — a bout won in two never fights a third, and
    /// <see cref="ComputePeriods"/> would misread that silence as a 0-0 tie.
    /// </remarks>
    internal static async Task<IResult> HandleFromEventsAsync(
        Guid id,
        SportFrogDbContext database,
        MatchRulesLookup rulesLookup,
        ResultPolicy policy,
        IMatchOutcomeRulesRegistry outcomeRules,
        ActiveSanctions activeSanctions,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (match, rules, refusal) = await LoadAsync(id, database, rulesLookup, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var (periods, _, deriveRefusal) =
            await TryDerivePeriodsFromEventsAsync(match!, rules!, database, cancellationToken);

        if (deriveRefusal is not null)
        {
            return deriveRefusal;
        }

        var violations = policy.Inspect(rules!, periods!);

        if (violations.Count > 0)
        {
            // Reachable for real here, unlike the cumulative-only case this
            // comment used to describe: a kyorugi bout still short of its
            // deciding asalto, or one recorded past it, fails exactly this
            // check rather than an outcome check upstream, because
            // ComputePlayedPeriods can only report what was recorded, not
            // whether it was recorded correctly.
            return Refuse(violations);
        }

        if (RefuseUnpriceableTie(match!, rules!, periods!) is { } unpriceable)
        {
            return unpriceable;
        }

        return await FinishAsync(
            match!, rules!, periods!, notes: null, database, outcomeRules, activeSanctions, organization, clock,
            cancellationToken);
    }

    /// <summary>
    /// A match's periods, read from its own recorded events, when that is a
    /// question this sport's score mode can even answer.
    /// </summary>
    /// <remarks>
    /// The one place <see cref="HandleFromEventsAsync"/> and
    /// <see cref="RefuseIfContradictsEventsAsync"/> both ask the same
    /// question — whether this match's own events say anything, and what —
    /// so a rule about reading events only has to be right once.
    ///
    /// <paramref name="match"/> is never queried from here: both callers
    /// already hold one <see cref="LoadAsync"/> or a caller of its own
    /// resolved, and re-reading it here would risk reading a second,
    /// possibly different copy of the same row inside the one request.
    /// </remarks>
    /// <returns>
    /// <see cref="HasEvents"/> is false exactly when nothing qualifying was
    /// recorded for this match at all — distinct from <paramref name="Periods"/>
    /// being non-null but describing a cumulative sport's silent, all-zero
    /// periods, which is what zero events still produce for one. A caller
    /// deciding whether there is anything to compare against needs this,
    /// not just whether periods came back.
    /// </returns>
    internal static async Task<(IReadOnlyList<PeriodScore>? Periods, bool HasEvents, IResult? Refusal)>
        TryDerivePeriodsFromEventsAsync(
            Match match,
            MatchRules rules,
            SportFrogDbContext database,
            CancellationToken cancellationToken)
    {
        var captureLevel = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.Id == match.Id)
            .Select(candidate => candidate.Competition!.CaptureLevel)
            .SingleAsync(cancellationToken);

        if (captureLevel != CaptureLevel.Detailed)
        {
            // Mirrors EventPolicy.RefuseMatch: a competition that only
            // records the score never has an event to read in the first
            // place, so there is nothing here to derive from — the manual
            // form is the only way in, same as it always was for this
            // competition.
            return (null, false, Results.Problem(
                detail: "Esta competencia solo registra el marcador. No hay eventos de los que derivar un " +
                        "resultado — cargá el resultado a mano.",
                statusCode: StatusCodes.Status409Conflict));
        }

        var enabledMetrics = rules.Configuration.Metrics;

        var puedeDerivarseDeEventos = await database.SportMetrics
            .AsNoTracking()
            .AnyAsync(
                metric => metric.SportCode == rules.Sport.Code
                    && metric.AffectsScore
                    && (enabledMetrics == null || enabledMetrics.Contains(metric.Code)),
                cancellationToken);

        if (!puedeDerivarseDeEventos)
        {
            return (null, false, Results.Problem(
                detail: $"{rules.Sport.Name} no tiene, bajo este reglamento, ningún evento que sume al " +
                        "marcador — no hay nada que sumar. Cargá el resultado a mano.",
                statusCode: StatusCodes.Status409Conflict));
        }

        var events = await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => recorded.MatchId == match.Id && recorded.Metric!.AffectsScore)
            .Select(recorded => new ScoringEvent(
                recorded.RosterEntry!.TeamId,
                recorded.Metric!.ScorePoints,
                recorded.Metric.CountsForOpponent,
                recorded.Quantity,
                recorded.PeriodNumber))
            .ToListAsync(cancellationToken);

        var hasEvents = events.Count > 0;

        if (rules.Sport.ScoreMode == ScoreMode.Sets
            && events.Any(recorded => recorded.PeriodNumber is not { } period
                || period < 1 || period > rules.Configuration.Periods.Count))
        {
            // EventPolicy.InspectPeriod requires a period for every new or
            // corrected event of a sets-mode sport, so this is a row that
            // predates that rule, or one whose ruleset was reconfigured to
            // fewer periods after it was recorded — not something a
            // recorder using the app today can still produce. Refused here,
            // before LiveScore.ComputePlayedPeriods, because it cannot
            // refuse the same way: which set an unplaced event belongs to
            // is the question a derived result exists to answer, not one it
            // can answer for itself.
            return (null, hasEvents, Results.Problem(
                detail: $"Hay eventos de este partido sin {rules.Configuration.Periods.Label} asignado, o " +
                        $"con uno que ya no existe. No se puede derivar un resultado por " +
                        $"{PeriodLabel.Plural(rules.Configuration.Periods.Label)} sin saber a cuál perteneció " +
                        "cada evento — corregilos en la lista de eventos del partido, o cargá el resultado " +
                        "a mano.",
                statusCode: StatusCodes.Status409Conflict));
        }

        // LoadAsync already refused a match without both teams, so both are
        // safe to unwrap here.
        var periods = rules.Sport.ScoreMode == ScoreMode.Sets
            ? LiveScore.ComputePlayedPeriods(
                events, rules.Configuration.Periods.Count, match.HomeTeamId!.Value, match.AwayTeamId!.Value)
            : LiveScore.ComputePeriods(
                events, rules.Configuration.Periods.Count, match.HomeTeamId!.Value, match.AwayTeamId!.Value);

        return (periods, hasEvents, null);
    }

    /// <summary>
    /// The match and its rules, or the one reason neither request can proceed
    /// without them.
    /// </summary>
    private static async Task<(Match? Match, MatchRules? Rules, IResult? Refusal)> LoadAsync(
        Guid id,
        SportFrogDbContext database,
        MatchRulesLookup rulesLookup,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (match is null)
        {
            return (null, null, Results.NotFound());
        }

        if (match.Status is MatchState.Cancelled or MatchState.Walkover)
        {
            // A cancelled match produced nothing, and a walkover was awarded
            // without being played. Giving either a score would contradict the
            // reason it holds the state it does.
            return (null, null, Results.Problem(
                detail: match.Status == MatchState.Cancelled
                    ? "Este partido fue cancelado, así que no tiene resultado. Ponelo de nuevo " +
                      "en el calendario primero si se va a jugar."
                    : "Este partido se otorgó por walkover. No se jugó nada, así que no hay " +
                      "marcador que registrar.",
                statusCode: StatusCodes.Status409Conflict));
        }

        if (!match.HasBothTeams)
        {
            // A knockout drawn in full names this fixture's round and phase
            // before it names its teams — one or both sides are still
            // "whoever wins another match", and there is nobody yet to credit
            // a score to.
            return (null, null, Results.Problem(
                detail: "Este partido todavía no tiene los dos equipos definidos: espera a que " +
                        "termine el partido anterior de la llave.",
                statusCode: StatusCodes.Status409Conflict));
        }

        if (await rulesLookup.FindRulesAsync(match, cancellationToken) is not { } rules)
        {
            return (null, null, Results.Problem(
                detail: "No se pueden leer las reglas de este partido, así que su resultado no se puede juzgar.",
                statusCode: StatusCodes.Status409Conflict));
        }

        return (match, rules, null);
    }

    private static IResult Refuse(IReadOnlyList<ResultViolation> violations) =>
        Results.ValidationProblem(violations
            .GroupBy(violation => violation.Property)
            .ToDictionary(
                group => group.Key,
                group => group.Select(violation => violation.Message).ToArray()));

    /// <summary>Applies a set of periods to a match and closes it.</summary>
    private static async Task<IResult> FinishAsync(
        Match match,
        MatchRules rules,
        IReadOnlyList<PeriodScore> periods,
        string? notes,
        SportFrogDbContext database,
        IMatchOutcomeRulesRegistry outcomeRules,
        ActiveSanctions activeSanctions,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (home, away) = outcomeRules.For(rules.Sport.ScoreMode).Consolidate(periods);

        try
        {
            await ApplyResultAsync(
                match, home, away, periods, notes, database, organization, clock, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else saved a result for this same match between when
            // this request read it and when it tried to save — the exact
            // race two scorekeepers submitting near-simultaneously produces.
            // Reported as a conflict instead of silently keeping whichever
            // save happened to land last, which is what this used to do.
            return Results.Problem(
                detail: "Alguien más cargó o corrigió el resultado de este partido mientras vos lo " +
                        "tenías abierto. Volvé a leerlo y aplicá tu corrección de nuevo si todavía hace falta.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Read after the match is already saved: neither list changes what
        // was just recorded, and the match being finished is what makes
        // asking worthwhile — see RecordResult.FinishAsync's own class
        // remarks on why this happens at the end of a match, not at the
        // moment a card is shown.
        var suggestedSanctions = await SuggestedSanctionsAsync(match, database, cancellationToken);
        var suspendedPlayerWarnings = await SuspendedPlayerWarningsAsync(match, database, activeSanctions, cancellationToken);

        return Results.Ok(new Response(home, away, suggestedSanctions, suspendedPlayerWarnings));
    }

    /// <summary>
    /// The mechanical half of finishing a match — setting its score and
    /// propagating a knockout winner — split from <see cref="FinishAsync"/>
    /// so a caller with no HTTP response to build (<c>EventDrivenResult</c>,
    /// keeping an already-finished match's score lined up with its events)
    /// can apply the same score the same way without computing the
    /// discipline follow-ups nobody there would read.
    /// </summary>
    /// <remarks>
    /// Saves on its own, same as before this was split out: the decided
    /// match and whatever <see cref="BracketWinnerPropagation"/> unlocks
    /// commit together in one round trip. Throws
    /// <see cref="DbUpdateConcurrencyException"/> rather than answering it —
    /// what that means, and to whom, is the caller's business: a 409 to
    /// report back for <see cref="FinishAsync"/>, a resync to quietly skip
    /// for <c>EventDrivenResult</c>.
    /// </remarks>
    internal static async Task ApplyResultAsync(
        Match match,
        int homeTotal,
        int awayTotal,
        IReadOnlyList<PeriodScore> periods,
        string? notes,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        match.PeriodScores = [.. periods.OrderBy(period => period.Period)];
        match.HomeTotal = homeTotal;
        match.AwayTotal = awayTotal;
        match.Status = MatchState.Finished;

        // A corrected score is not the score the shootout was played to
        // settle — even a correction that is still level needed a fresh one,
        // since the two sides taking the kicks is itself part of what the
        // shootout answered.
        match.PenaltyHomeScore = null;
        match.PenaltyAwayScore = null;

        if (notes is not null)
        {
            match.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        }

        // Who recorded it and who changed it afterwards are different
        // questions, and a competition asks the second one out loud when a
        // score is disputed. The first author is never overwritten.
        var now = clock.GetUtcNow();

        if (match.RecordedBy is null)
        {
            match.RecordedBy = organization.UserId;
            match.RecordedAt = now;
        }
        else
        {
            match.ModifiedBy = organization.UserId;
            match.ModifiedAt = now;
        }

        await BracketWinnerPropagation.ApplyAsync(match, database, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Red card code from the sport catalog — see <c>Features.Lists.Providers.CardsList</c>'s own narrower list for the sibling that also reads yellow cards.</summary>
    private const string RedCardMetricCode = "red_card";

    /// <summary>
    /// Everybody sent off in this match who nobody has sanctioned for it
    /// yet.
    /// </summary>
    /// <remarks>
    /// "For it" is answered by <see cref="Sanction.MatchId"/>: a sanction
    /// already pointing at this match, for this person, is not suggested
    /// again the next time a result is corrected. A sanction entered without
    /// linking the match is not matched here and would surface once more —
    /// a redundant suggestion is a click to dismiss, not a wrong one.
    /// </remarks>
    private static async Task<IReadOnlyList<SuggestedSanction>> SuggestedSanctionsAsync(
        Match match, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        var sentOff = await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => recorded.MatchId == match.Id && recorded.Metric!.Code == RedCardMetricCode)
            .Select(recorded => new
            {
                recorded.RosterEntry!.AthleteId,
                recorded.RosterEntry.TeamId,
                recorded.RosterEntry.Athlete!.FirstName,
                recorded.RosterEntry.Athlete.LastName,
                TeamName = recorded.RosterEntry.Team!.Name,
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sentOff.Count == 0)
        {
            return [];
        }

        var alreadySanctioned = await database.Sanctions
            .AsNoTracking()
            .Where(sanction => sanction.MatchId == match.Id)
            .Select(sanction => sanction.AthleteId)
            .ToListAsync(cancellationToken);

        return sentOff
            .Where(player => !alreadySanctioned.Contains(player.AthleteId))
            .Select(player => new SuggestedSanction(
                player.AthleteId, player.FirstName, player.LastName, player.TeamId, player.TeamName, match.Id))
            .ToList();
    }

    /// <summary>
    /// Everybody on either side's active roster who carries a sanction that
    /// blocks them today — see this method's own <see cref="SuspendedPlayerWarning"/>
    /// for why this asks the roster rather than the events.
    /// </summary>
    private static async Task<IReadOnlyList<SuspendedPlayerWarning>> SuspendedPlayerWarningsAsync(
        Match match, SportFrogDbContext database, ActiveSanctions activeSanctions, CancellationToken cancellationToken)
    {
        var roster = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.TeamId == match.HomeTeamId || entry.TeamId == match.AwayTeamId)
            .Select(entry => new { entry.AthleteId, entry.TeamId, TeamName = entry.Team!.Name })
            .ToListAsync(cancellationToken);

        if (roster.Count == 0)
        {
            return [];
        }

        var sanctioned = await activeSanctions.ForAthletesAsync(
            match.CompetitionId, roster.Select(entry => entry.AthleteId).Distinct().ToList(), cancellationToken);

        if (sanctioned.Count == 0)
        {
            return [];
        }

        // One roster row per athlete is all a single competition's own
        // rules allow; GroupBy/First is a defensive read, not a sign this
        // ever actually happens.
        var rosterByAthlete = roster
            .GroupBy(entry => entry.AthleteId)
            .ToDictionary(group => group.Key, group => group.First());

        return sanctioned
            .Select(sanction => new SuspendedPlayerWarning(
                sanction.AthleteId,
                sanction.AthleteFirstName,
                sanction.AthleteLastName,
                rosterByAthlete[sanction.AthleteId].TeamId,
                rosterByAthlete[sanction.AthleteId].TeamName,
                sanction.Reason))
            .ToList();
    }
}
