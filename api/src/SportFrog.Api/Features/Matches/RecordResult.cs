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
            match!, rules!, request.PeriodScores, request.Notes, database, organization, clock, cancellationToken);
    }

    /// <summary>
    /// Finishes a match without asking anyone to type a score: the events
    /// already logged against it — every goal, tagged with the period it was
    /// scored in — are exactly what a period-by-period result says, added up
    /// instead of remembered.
    /// </summary>
    /// <remarks>
    /// Refused for a sport decided in sets. Under
    /// <see cref="ScoreMode.Sets"/> nothing recorded during the match affects
    /// the score — a volleyball point is a statistic, not a goal — so there is
    /// nothing here to tally from and the manual form is the only way in.
    /// </remarks>
    private static async Task<IResult> HandleFromEventsAsync(
        Guid id,
        SportFrogDbContext database,
        MatchRulesLookup rulesLookup,
        ResultPolicy policy,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (match, rules, refusal) = await LoadAsync(id, database, rulesLookup, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (rules!.Sport.ScoreMode == ScoreMode.Sets)
        {
            return Results.Problem(
                detail: $"{rules.Sport.Name} se decide por {rules.Configuration.Periods.Label.ToLowerInvariant()}s " +
                        "ganados, no por goles cargados como eventos — no hay nada que sumar. Cargá el " +
                        "resultado a mano.",
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

        var periods = LiveScore.ComputePeriods(
            events, rules.Configuration.Periods.Count, match!.HomeTeamId, match.AwayTeamId);

        var violations = policy.Inspect(rules, periods);

        if (violations.Count > 0)
        {
            // Only reachable if the ruleset's period count changed between
            // two people opening the same "finish" button, or something else
            // this file does not otherwise guard against — ComputePeriods
            // always emits exactly the periods Inspect asks for.
            return Refuse(violations);
        }

        return await FinishAsync(match, rules, periods, notes: null, database, organization, clock, cancellationToken);
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
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (home, away) = ScoreConsolidation.Consolidate(rules.Sport.ScoreMode, periods);

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

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(home, away));
    }
}
