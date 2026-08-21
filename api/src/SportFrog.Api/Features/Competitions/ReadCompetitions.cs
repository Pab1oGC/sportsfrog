using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Lists the competitions of the active organization, or reads one.
///
/// Neither query filters by organization: the isolation context already
/// restricts what the database will return, and repeating the filter here
/// would suggest the guarantee lives in this code.
/// </summary>
public static class ReadCompetitions
{
    public sealed record Summary(
        Guid Id,
        string SportCode,
        Guid RulesetId,
        string RulesetName,
        string Name,
        string Slug,
        string Season,
        string Format,
        CaptureLevel CaptureLevel,
        CompetitionState Status,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        bool IsPublic,
        CompetitionSettings Settings);

    public static IEndpointRouteBuilder MapReadCompetitions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCompetitions))
            .WithSummary("Lists the competitions of the active organization.");

        routes.MapGet("/competitions/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadCompetition")
            .WithSummary("Reads one competition.");

        return routes;
    }

    /// <summary>
    /// The organization's competitions, newest season first.
    /// </summary>
    /// <remarks>
    /// The status filter is what makes this list usable after a few seasons:
    /// the question is almost always "what is running now", and an
    /// organization that has been on the platform for three years has far
    /// more finished competitions than live ones.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? status = null,
        string? sport = null,
        string? search = null)
    {
        // A query parameter reaches no validator — the contract filter only
        // sees arguments that have one — so an unrecognized state is answered
        // here, and answered the same way a bad field in a body would be.
        // Bound as an enum it would come back as a bare 400 with no body at
        // all, which tells the caller nothing about which of three parameters
        // it disliked.
        CompetitionState? state = null;

        sport = QueryFilter.OrAbsent(sport);
        search = QueryFilter.OrAbsent(search);

        if (QueryFilter.OrAbsent(status) is { } requested)
        {
            if (!WireEnum.TryParse<CompetitionState>(requested, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] =
                    [
                        $"Unknown state. Available: {WireEnum.Options<CompetitionState>()}.",
                    ],
                });
            }

            state = parsed;
        }

        return Results.Ok(await Project(database.Competitions
                .Where(competition => state == null || competition.Status == state)
                .Where(competition => sport == null || competition.SportCode == sport)
                .Where(competition => search == null
                    || EF.Functions.ILike(competition.Name, $"%{search}%"))
                .OrderByDescending(competition => competition.Season)
                .ThenBy(competition => competition.Name))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var competition = await Project(database.Competitions.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return competition is null ? Results.NotFound() : Results.Ok(competition);
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same competition differently.
    /// </summary>
    /// <remarks>
    /// The ruleset's name travels alongside its identifier because that is
    /// what a competition is recognized by in an interface. Reading it here
    /// costs a join the database was going to make anyway, and saves the
    /// caller a second request for a single string.
    /// </remarks>
    private static IQueryable<Summary> Project(IQueryable<Competition> competitions) =>
        competitions.Select(competition => new Summary(
            competition.Id,
            competition.SportCode,
            competition.RulesetId,
            competition.Ruleset!.Name,
            competition.Name,
            competition.Slug,
            competition.Season,
            competition.Format,
            competition.CaptureLevel,
            competition.Status,
            competition.StartsOn,
            competition.EndsOn,
            competition.IsPublic,
            competition.Settings));
}
