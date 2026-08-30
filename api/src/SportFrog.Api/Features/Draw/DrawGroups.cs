using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws which group each team of a category lands in — the half of a group
/// stage that happens before <see cref="DrawCalendar"/> has fixtures to draw
/// at all.
/// </summary>
/// <remarks>
/// A separate operation from <see cref="DrawCalendar"/> rather than a step
/// inside it, because the two answer different questions with different
/// inputs: how many groups, against how many teams there are, versus how
/// many legs a round-robin plays. Keeping them apart also keeps a category
/// that already has its groups set by hand — a league that seeds them itself
/// rather than trusting a lottery — free to skip this and go straight to
/// drawing fixtures, which is exactly what a group stage without groups
/// already meant before this existed.
/// </remarks>
public static class DrawGroups
{
    /// <param name="RespectPots">
    /// True is the ordinary case: no group ever gets two teams that share a
    /// pot. False draws every team from one shared pool regardless of pots —
    /// for a league phase that deliberately lets its own favourites meet,
    /// the way the newer Champions League format does.
    /// </param>
    public sealed record Request(int GroupCount, bool RespectPots = true);

    public sealed record TeamAssignment(Guid TeamId, string TeamName, short? Seed, string GroupLabel);

    public sealed record Response(IReadOnlyList<TeamAssignment> Teams);

    /// <summary>
    /// Past this, group labels would run out of single letters — a
    /// competition with more groups than that is not one this draws by
    /// lottery, it is one somebody organizes by hand.
    /// </summary>
    private const int MaximumGroups = 26;

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.GroupCount)
                .InclusiveBetween(2, MaximumGroups)
                .WithMessage($"Un sorteo arma entre 2 y {MaximumGroups} grupos.");
    }

    public static IEndpointRouteBuilder MapDrawGroups(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/draw/groups", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DrawGroups))
            .WithSummary("Draws every team of a category into a group.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        if (competition.Format != CompetitionFormat.Groups)
        {
            return Results.Problem(
                detail: $"Esta operación sortea equipos en grupos. Esta competencia está " +
                        $"sorteada como {competition.Format}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            return Results.Problem(
                detail: "Esta competencia ya está en curso, así que sus grupos no se pueden " +
                        "volver a sortear.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // The same rule DrawCalendar applies to its own redraw: once a match
        // has a result, reseating teams into different groups would leave it
        // pointing at a group table it never actually belonged to.
        var settled = await database.Matches
            .AsNoTracking()
            .AnyAsync(
                match => match.CategoryId == categoryId
                    && (match.Status == MatchState.Finished || match.Status == MatchState.Walkover),
                cancellationToken);

        if (settled)
        {
            return Results.Problem(
                detail: "Ya se registraron resultados en esta categoría, así que sus grupos no " +
                        "se pueden volver a sortear.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var teams = await database.Teams
            .Where(team => team.CategoryId == categoryId && team.IsActive)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);

        var seeded = teams.Select(team => new SeededTeam(team.Id, team.Seed)).ToList();

        var (assignments, problem) = GroupDraw.Draw(seeded, request.GroupCount, request.RespectPots);

        if (assignments is null)
        {
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status409Conflict);
        }

        var labelByTeam = assignments.ToDictionary(assignment => assignment.TeamId, assignment => assignment.GroupLabel);

        foreach (var team in teams)
        {
            team.GroupLabel = labelByTeam[team.Id];
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            [.. teams
                .OrderBy(team => team.GroupLabel, StringComparer.Ordinal)
                .ThenBy(team => team.Name)
                .Select(team => new TeamAssignment(team.Id, team.Name, team.Seed, team.GroupLabel!))]));
    }
}
