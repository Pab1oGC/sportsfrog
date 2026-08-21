using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Lists the rulesets of the active organization, or reads one.
///
/// Neither query filters by organization: the isolation context already
/// restricts what the database will return, and repeating the filter here
/// would suggest the guarantee lives in this code.
/// </summary>
public static class ReadRulesets
{
    public sealed record Summary(
        Guid Id,
        string SportCode,
        string Name,
        RulesetConfiguration Config,
        DateTimeOffset UpdatedAt);

    public static IEndpointRouteBuilder MapReadRulesets(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/rulesets", ListAsync)
            // Read by anyone in the organization, written by an administrator.
            // Whoever runs a fixture has to be able to look up what a draw is
            // worth without being able to change it.
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadRulesets))
            .WithSummary("Lists the rulesets of the active organization.");

        routes.MapGet("/rulesets/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadRuleset")
            .WithSummary("Reads one ruleset.");

        return routes;
    }

    /// <summary>
    /// The organization's rulesets, narrowed by sport or by name.
    /// </summary>
    /// <remarks>
    /// The sport filter is the reason this list is usually read: a
    /// competition is being set up for one sport, and offering the volleyball
    /// rulesets among the football ones invites the mistake the whole module
    /// exists to prevent.
    ///
    /// The configuration comes with each entry rather than only on the single
    /// read. An organization holds a handful of these, and the choice between
    /// two of them is made on what they say — three points a win, ties broken
    /// by goal difference — which is a poor thing to hide behind another call.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? sport = null,
        string? search = null) =>
        Results.Ok(await Project(database.Rulesets
                .Where(ruleset => sport == null || ruleset.SportCode == sport)
                .Where(ruleset => search == null || EF.Functions.ILike(ruleset.Name, $"%{search}%"))
                .OrderBy(ruleset => ruleset.SportCode)
                .ThenBy(ruleset => ruleset.Name))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var ruleset = await Project(database.Rulesets.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        // A ruleset of another organization is not found rather than
        // forbidden, and that is not a choice made here: the policy never
        // returned it, so there is nothing to distinguish it from one that
        // does not exist.
        return ruleset is null ? Results.NotFound() : Results.Ok(ruleset);
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same ruleset differently.
    /// </summary>
    private static IQueryable<Summary> Project(IQueryable<Ruleset> rulesets) =>
        rulesets.Select(ruleset => new Summary(
            ruleset.Id,
            ruleset.SportCode,
            ruleset.Name,
            ruleset.Config,
            ruleset.UpdatedAt));
}
