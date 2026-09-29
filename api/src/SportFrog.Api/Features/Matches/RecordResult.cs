using FluentValidation;
using Microsoft.EntityFrameworkCore;
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

    /// <param name="HomeTotal">
    /// The match score, consolidated. Returned because it is derived rather
    /// than sent, and the caller has no other way to learn what their periods
    /// added up to.
    /// </param>
    public sealed record Response(int HomeTotal, int AwayTotal);

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
            .WithName(nameof(RecordResult))
            .WithSummary("Records the result of a match.");

        routes.MapPost("/matches/{id:guid}/result/from-events", HandleFromEventsAsync)
            .RequireRole(MembershipRole.Recorder)
            .WithName("RecordResultFromEvents")
            .WithSummary("Finishes a match with the score tallied from its recorded events.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        MatchRulesLookup rulesLookup,
        ResultPolicy policy,
        IMatchOutcomeRulesRegistry outcomeRules,
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

        return await FinishAsync(
            match!, rules!, request.PeriodScores, request.Notes, database, outcomeRules, organization, clock,
            cancellationToken);
    }

    /// <summary>
    /// Finishes a match without asking anyone to type a score: the events
    /// already logged against it — every goal, or every taekwondo point and
    /// gam-jeom, tagged with the period it happened in — are exactly what a
    /// period-by-period result says, added up instead of remembered.
    /// </summary>
    /// <remarks>
    /// Refused only when this sport's ruleset has no metric that actually
    /// moves the score — a volleyball point is a statistic, not something to
    /// sum, so there is nothing here to tally and the manual form is the
    /// only way in. That used to be every sport decided in sets, but it no
    /// longer is: a taekwondo kyorugi point or gam-jeom does affect the score
    /// (see <c>AddTaekwondoKyorugiScoringEvents</c>), so this now asks the
    /// catalog directly instead of assuming the answer from the score mode.
    ///
    /// A sets-mode sport that does qualify still is not a cumulative one:
    /// <see cref="LiveScore.ComputePlayedPeriods"/> reports only the asaltos
    /// something was actually recorded for, not every one the ruleset
    /// configures — a bout won in two never fights a third, and
    /// <see cref="ComputePeriods"/> would misread that silence as a 0-0 tie.
    /// </remarks>
    private static async Task<IResult> HandleFromEventsAsync(
        Guid id,
        SportFrogDbContext database,
        MatchRulesLookup rulesLookup,
        ResultPolicy policy,
        IMatchOutcomeRulesRegistry outcomeRules,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (match, rules, refusal) = await LoadAsync(id, database, rulesLookup, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var enabledMetrics = rules!.Configuration.Metrics;

        var puedeDerivarseDeEventos = await database.SportMetrics
            .AsNoTracking()
            .AnyAsync(
                metric => metric.SportCode == rules.Sport.Code
                    && metric.AffectsScore
                    && (enabledMetrics == null || enabledMetrics.Contains(metric.Code)),
                cancellationToken);

        if (!puedeDerivarseDeEventos)
        {
            return Results.Problem(
                detail: $"{rules.Sport.Name} no tiene, bajo este reglamento, ningún evento que sume al " +
                        "marcador — no hay nada que sumar. Cargá el resultado a mano.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var events = await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => recorded.MatchId == id && recorded.Metric!.AffectsScore)
            .Select(recorded => new ScoringEvent(
                recorded.RosterEntry!.TeamId,
                recorded.Metric!.ScorePoints,
                recorded.Metric.CountsForOpponent,
                recorded.Quantity,
                recorded.PeriodNumber))
            .ToListAsync(cancellationToken);

        // LoadAsync already refused a match without both teams, so both are
        // safe to unwrap here.
        var periods = rules.Sport.ScoreMode == ScoreMode.Sets
            ? LiveScore.ComputePlayedPeriods(
                events, rules.Configuration.Periods.Count, match!.HomeTeamId!.Value, match.AwayTeamId!.Value)
            : LiveScore.ComputePeriods(
                events, rules.Configuration.Periods.Count, match!.HomeTeamId!.Value, match.AwayTeamId!.Value);

        var violations = policy.Inspect(rules, periods);

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

        return await FinishAsync(
            match, rules, periods, notes: null, database, outcomeRules, organization, clock, cancellationToken);
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
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (home, away) = outcomeRules.For(rules.Sport.ScoreMode).Consolidate(periods);

        match.PeriodScores = [.. periods.OrderBy(period => period.Period)];
        match.HomeTotal = home;
        match.AwayTotal = away;
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

        try
        {
            await database.SaveChangesAsync(cancellationToken);
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

        return Results.Ok(new Response(home, away));
    }
}
