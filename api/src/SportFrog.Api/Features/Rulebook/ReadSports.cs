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
    /// The ordering is applied before the projection and not after it: past
    /// the Select the sequence is one of anonymous summaries carrying a
    /// nested collection, which the provider cannot sort in the database and
    /// refuses to sort silently in memory.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken) =>
        Results.Ok(await Project(database.Sports.OrderBy(sport => sport.Name))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadAsync(
        string code,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var sport = await Project(database.Sports.Where(candidate => candidate.Code == code))
            .SingleOrDefaultAsync(cancellationToken);

        return sport is null ? Results.NotFound() : Results.Ok(sport);
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same sport differently.
    /// </summary>
    private static IQueryable<Summary> Project(IQueryable<Sport> sports) =>
        sports.Select(sport => new Summary(
            sport.Code,
            sport.Name,
            sport.PeriodLabel,
            sport.DefaultPeriods,
            sport.ScoringUnit,
            sport.ScoreMode == ScoreMode.Sets ? "sets" : "cumulative",
            sport.Metrics
                .OrderBy(metric => metric.DisplayOrder)
                .Select(metric => new MetricSummary(
                    metric.Id,
                    metric.Code,
                    metric.Label,
                    metric.AffectsScore,
                    metric.IsRankable))
                .ToList()));
}
