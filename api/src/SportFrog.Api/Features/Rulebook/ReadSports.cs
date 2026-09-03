using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Reads the shared sports catalog.
///
/// There is no write side on purpose. The catalog is the set of sports the
/// platform knows how to score, consolidate and rank; extending it is a
/// change to the product, delivered by migration, and not a setting an
/// organization can reach.
///
/// It is still served behind the organization context like everything else.
/// The content is the same for every caller, so nothing is being protected
/// here — but exempting an endpoint from the context is a decision that
/// should be worth arguing for, and "the answer happens to be public" is not
/// a reason to open a second class of route.
/// </summary>
public static class ReadSports
{
    public sealed record MetricSummary(
        Guid Id,
        string Code,
        string Label,
        bool AffectsScore,
        bool IsRankable);

    public sealed record Summary(
        string Code,
        string Name,
        string PeriodLabel,
        short DefaultPeriods,
        string ScoringUnit,
        string ScoreMode,

        /// <summary>
        /// Whether a match is decided by periods won rather than by a total
        /// score. The one fact the client actually branches on — whether the
        /// score comes from summed events or has to be entered period by
        /// period, whether a walkover's score is fixed by the period count
        /// or freely chosen — named for that, not for which raw
        /// <see cref="Rules.ScoreMode"/> string it happens to come from.
        /// A future score mode still answers this one question honestly
        /// instead of quietly matching neither string a two-way check
        /// expects.
        /// </summary>
        bool IsPlayedInSets,

        /// <summary>
        /// The outcomes a ruleset for this sport must price, at its default
        /// period count — win/loss for a cumulative sport, every scoreline a
        /// best-of-<see cref="DefaultPeriods"/> match can finish on for one
        /// played in sets. The same keys <c>RulesetPolicy</c> requires, so a
        /// client building a ruleset form never has to guess the shape.
        /// </summary>
        IReadOnlyCollection<string> RequiredOutcomes,

        /// <summary>Outcomes a ruleset for this sport may price, but need not.</summary>
        IReadOnlyCollection<string> OptionalOutcomes,

        IReadOnlyCollection<MetricSummary> Metrics);

    public static IEndpointRouteBuilder MapReadSports(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/sports", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadSports))
            .WithSummary("Lists the sports the platform supports.");

        routes.MapGet("/sports/{code}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadSport")
            .WithSummary("Reads one sport and its metrics.");

        return routes;
    }

    /// <summary>
    /// Every sport, with its metrics.
    /// </summary>
    /// <remarks>
    /// Metrics come embedded rather than behind a second call: the catalog is
    /// a handful of rows, and the reason to read it at all is to build a
    /// ruleset, which needs the metrics to choose from. Making the client ask
    /// twice for something this small would buy nothing.
    ///
    /// Sorted in the query and materialized before <see cref="Project"/> runs:
    /// deriving the priced outcomes calls into <c>IMatchOutcomeRulesRegistry</c>,
    /// which the database provider has no way to translate to SQL, so it has
    /// to run against real objects rather than inside the query expression.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        IMatchOutcomeRulesRegistry outcomeRules,
        CancellationToken cancellationToken)
    {
        var sports = await database.Sports
            .AsNoTracking()
            .Include(sport => sport.Metrics.OrderBy(metric => metric.DisplayOrder))
            .OrderBy(sport => sport.Name)
            .ToListAsync(cancellationToken);

        return Results.Ok(sports.Select(sport => Project(sport, outcomeRules)));
    }

    private static async Task<IResult> ReadAsync(
        string code,
        SportFrogDbContext database,
        IMatchOutcomeRulesRegistry outcomeRules,
        CancellationToken cancellationToken)
    {
        var sport = await database.Sports
            .AsNoTracking()
            .Include(candidate => candidate.Metrics.OrderBy(metric => metric.DisplayOrder))
            .SingleOrDefaultAsync(candidate => candidate.Code == code, cancellationToken);

        return sport is null ? Results.NotFound() : Results.Ok(Project(sport, outcomeRules));
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same sport differently.
    /// </summary>
    internal static Summary Project(Sport sport, IMatchOutcomeRulesRegistry outcomeRules)
    {
        var rules = outcomeRules.For(sport.ScoreMode);

        return new Summary(
            sport.Code,
            sport.Name,
            sport.PeriodLabel,
            sport.DefaultPeriods,
            sport.ScoringUnit,
            sport.ScoreMode == ScoreMode.Sets ? "sets" : "cumulative",
            sport.ScoreMode == ScoreMode.Sets,
            rules.RequiredOutcomes(sport.DefaultPeriods),
            rules.OptionalOutcomes(),
            sport.Metrics
                .Select(metric => new MetricSummary(
                    metric.Id,
                    metric.Code,
                    metric.Label,
                    metric.AffectsScore,
                    metric.IsRankable))
                .ToList());
    }
}
