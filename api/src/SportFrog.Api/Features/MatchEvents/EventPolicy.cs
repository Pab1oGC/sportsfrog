using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.MatchEvents;

/// <summary>Something an event says that its match does not allow.</summary>
internal sealed record EventViolation(string Property, string Message);

/// <summary>
/// Whether something can be recorded as having happened in a match.
/// </summary>
/// <remarks>
/// Two of these checks the schema explicitly hands over. Its own comment on
/// <c>player_events</c> says the metric-to-sport correspondence "is validated
/// in the application layer: the constraint can't be expressed as a foreign
/// key without denormalizing the sport onto this table" — so nothing stops a
/// football match from recording a basketball rebound except this.
///
/// The other is the player. A foreign key says the registration exists; it
/// cannot say the registration belongs to one of the two teams on the field,
/// and a goal credited to somebody from another division would satisfy every
/// key on the row.
/// </remarks>
internal sealed class EventPolicy(SportFrogDbContext database)
{
    /// <summary>What a match will accept, gathered once.</summary>
    internal sealed record MatchContext(
        Match Match,
        string SportCode,
        CaptureLevel CaptureLevel,
        RulesetConfiguration Rules);

    /// <summary>
    /// The match and everything needed to judge what happened in it, or null
    /// when there is no such match.
    /// </summary>
    public async Task<MatchContext?> FindContextAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var context = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.Id == matchId)
            .Select(candidate => new
            {
                candidate.Competition!.SportCode,
                candidate.Competition.CaptureLevel,

                // The category's override where it has one, the competition's
                // otherwise: which metrics may be recorded is part of the
                // rules a division plays by.
                RulesetId = candidate.Category!.RulesetId ?? candidate.Competition.RulesetId,
            })
            .SingleAsync(cancellationToken);

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == context.RulesetId, cancellationToken);

        return ruleset is null
            ? null
            : new MatchContext(match, context.SportCode, context.CaptureLevel, ruleset.Config);
    }

    /// <summary>
    /// Whether the match is one that records events at all.
    /// </summary>
    /// <remarks>
    /// Separate from the rest because it is about the fixture rather than
    /// about what is being recorded, and it answers before anything else is
    /// worth looking up.
    /// </remarks>
    public static IResult? RefuseMatch(MatchContext context)
    {
        if (context.CaptureLevel != CaptureLevel.Detailed)
        {
            // The competition chose to record scores and nothing else.
            // Accepting events anyway would build a top scorer table out of
            // whichever matches somebody happened to detail, and present it as
            // the season's.
            return Results.Problem(
                detail: "Esta competencia solo registra el marcador. Los eventos pertenecen a " +
                        "una competencia que captura detalle, y su nivel de captura queda fijo " +
                        "una vez que sale de borrador.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (context.Match.Status is not (MatchState.InProgress or MatchState.Finished))
        {
            // Nothing happened in a match that has not started, was called
            // off, or was awarded without being played.
            return Results.Problem(
                detail: "No se puede registrar nada para un partido que está " +
                        $"{WireEnum.Label(context.Match.Status)}. " +
                        "Iniciálo primero.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return null;
    }

    /// <summary>
    /// Everything wrong with one event, or nothing.
    /// </summary>
    public async Task<IReadOnlyList<EventViolation>> InspectAsync(
        MatchContext context,
        Guid rosterEntryId,
        Guid metricId,
        short? periodNumber,
        CancellationToken cancellationToken)
    {
        var violations = new List<EventViolation>();

        await InspectMetricAsync(context, metricId, violations, cancellationToken);
        await InspectPlayerAsync(context, rosterEntryId, violations, cancellationToken);

        InspectPeriod(context, periodNumber, violations);

        return violations;
    }

    /// <summary>
    /// The metric has to belong to the sport, and to what these rules record.
    /// </summary>
    private async Task InspectMetricAsync(
        MatchContext context,
        Guid metricId,
        List<EventViolation> violations,
        CancellationToken cancellationToken)
    {
        var metric = await database.SportMetrics
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == metricId, cancellationToken);

        if (metric is null)
        {
            violations.Add(new EventViolation("MetricId", "Ese evento no existe en el catálogo."));
            return;
        }

        if (metric.SportCode != context.SportCode)
        {
            violations.Add(new EventViolation(
                "MetricId",
                $"\"{metric.Label}\" es de {metric.SportCode}, y este partido es de " +
                $"{context.SportCode}."));
            return;
        }

        // An absent list means the ruleset records everything the sport
        // offers, which is the ordinary case.
        if (context.Rules.Metrics is { } enabled
            && !enabled.Contains(metric.Code, StringComparer.Ordinal))
        {
            violations.Add(new EventViolation(
                "MetricId",
                $"Este reglamento no registra \"{metric.Label}\". Registra: " +
                $"{string.Join(", ", enabled)}."));
        }
    }

    /// <summary>
    /// The player has to have been on one of the two teams, at the time.
    /// </summary>
    private async Task InspectPlayerAsync(
        MatchContext context,
        Guid rosterEntryId,
        List<EventViolation> violations,
        CancellationToken cancellationToken)
    {
        var entry = await database.RosterEntries
            .AsNoTracking()
            .Where(candidate => candidate.Id == rosterEntryId)
            .Select(candidate => new
            {
                candidate.TeamId,
                candidate.WithdrawnAt,
                Player = candidate.Athlete!.FirstName + " " + candidate.Athlete.LastName,
                TeamName = candidate.Team!.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            violations.Add(new EventViolation(
                "RosterEntryId", "Ninguna inscripción de esta organización tiene ese identificador."));
            return;
        }

        if (entry.TeamId != context.Match.HomeTeamId && entry.TeamId != context.Match.AwayTeamId)
        {
            violations.Add(new EventViolation(
                "RosterEntryId",
                $"{entry.Player} está inscripto en {entry.TeamName}, que no juega este partido."));
            return;
        }

        // A player who left in March did not score in April. Only checked
        // when both dates are known: a fixture with no date yet cannot be
        // compared against anything, and refusing on that would block
        // recording a match somebody forgot to schedule.
        if (entry.WithdrawnAt is { } left
            && context.Match.ScheduledAt is { } played
            && left < played)
        {
            violations.Add(new EventViolation(
                "RosterEntryId",
                $"{entry.Player} ya había dejado {entry.TeamName} cuando se jugó este partido."));
        }
    }

    /// <summary>
    /// The period has to be one the match has.
    /// </summary>
    private static void InspectPeriod(
        MatchContext context,
        short? periodNumber,
        List<EventViolation> violations)
    {
        if (periodNumber is not { } period)
        {
            // Not every organizer writes it down, and an event without a
            // period still counts towards a total.
            return;
        }

        var configured = context.Rules.Periods.Count;

        if (period < 1 || period > configured)
        {
            violations.Add(new EventViolation(
                "PeriodNumber",
                $"Este partido se juega en {PeriodLabel.Count(configured, context.Rules.Periods.Label)}, " +
                $"así que no existe el número {period}."));
        }
    }
}
