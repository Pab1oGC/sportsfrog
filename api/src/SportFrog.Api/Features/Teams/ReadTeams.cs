using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Teams;

/// <summary>
/// Lists the teams of a category, or reads one.
/// </summary>
/// <remarks>
/// The collection hangs off the category it belongs to; a single team is
/// addressed on its own. Repeating the whole chain — competition, category,
/// team — would put two identifiers in the path that can disagree with each
/// other, and the deeper one already answers the question.
/// </remarks>
public static class ReadTeams
{
    public sealed record Summary(
        Guid Id,
        Guid CategoryId,
        Guid ClubId,
        string ClubName,
        string Name,
        string? GroupLabel,
        short? Seed,
        bool IsActive,
        int RosterSize);

    public static IEndpointRouteBuilder MapReadTeams(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/categories/{categoryId:guid}/teams", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadTeams))
            .WithSummary("Lists the teams entered in a category.");

        routes.MapGet("/teams/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadTeam")
            .WithSummary("Reads one team.");

        return routes;
    }

    /// <summary>
    /// The teams of one category, grouped as they were drawn.
    /// </summary>
    /// <remarks>
    /// The category is checked first: one that does not exist and one with no
    /// teams entered yet are different answers, and both would otherwise come
    /// back as an empty list.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await database.Categories.AnyAsync(
                category => category.Id == categoryId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.Teams
                .Where(team => team.CategoryId == categoryId)
                .OrderBy(team => team.GroupLabel)
                .ThenBy(team => team.Name))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var team = await Project(database.Teams.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return team is null ? Results.NotFound() : Results.Ok(team);
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same team differently.
    /// </summary>
    /// <remarks>
    /// The squad size counts only players still on the team: not withdrawn,
    /// not struck off. It is the number a category caps, so answering it here
    /// keeps every caller from fetching a squad to count it — and from each
    /// of them deciding differently what counts.
    /// </remarks>
    private static IQueryable<Summary> Project(IQueryable<Team> teams) =>
        teams.Select(team => new Summary(
            team.Id,
            team.CategoryId,
            team.ClubId,
            team.Club!.Name,
            team.Name,
            team.GroupLabel,
            team.Seed,
            team.IsActive,
            team.Roster.Count(entry => entry.WithdrawnAt == null && entry.DeletedAt == null)));
}
