using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws the next round of a knockout from the winners of the last one.
/// </summary>
/// <remarks>
/// The half of a bracket that cannot be drawn in advance. Every round is a
/// fresh draw over whoever survived, which is why a knockout needs an
/// operation of its own rather than being a parameter of the first draw.
/// </remarks>
public static class AdvanceBracket
{
    /// <param name="Champion">
    /// Set when the last round was the final. Nothing is created; the answer
    /// to "what is the next round" is that there is not one.
    /// </param>
    public sealed record Response(
        int Created,
        int Round,
        string? Phase,
        Guid? Champion,
        string? ChampionName);

    public static IEndpointRouteBuilder MapAdvanceBracket(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/draw/next-round", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(AdvanceBracket))
            .WithSummary("Draws the next knockout round from the winners of the last.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        OrganizationContext organization,
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

        if (competition.Format != CompetitionFormat.Knockout)
        {
            return Results.Problem(
                detail: $"Rounds are advanced in a knockout. This competition is drawn as " +
                        $"{competition.Format}, where every fixture is known from the start.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var matches = await database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId)
            .Select(match => new
            {
                match.RoundNumber,
                match.Status,
                match.HomeTeamId,
                match.AwayTeamId,
                match.HomeTotal,
                match.AwayTotal,
                match.WalkoverTeamId,
            })
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            return Results.Problem(
                detail: "This category has no bracket yet. Draw its first round before advancing.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var round = matches.Max(match => match.RoundNumber) ?? 0;
        var last = matches.Where(match => match.RoundNumber == round).ToList();

        if (last.Any(match => match.Status is not (MatchState.Finished or MatchState.Walkover)))
        {
            return Results.Problem(
                detail: "Not every match of the current round has a result, so it is not known " +
                        "who plays the next one.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var winners = new List<Guid>();

        foreach (var match in last)
        {
            if (match.WalkoverTeamId is { } awarded)
            {
                winners.Add(awarded);
                continue;
            }

            if (match.HomeTotal == match.AwayTotal)
            {
                // A knockout has to produce somebody. A level result means the
                // tie was not actually decided — extra time, penalties or a
                // replay happened and were not recorded — and guessing a
                // winner here would put a team into the next round on the
                // strength of nothing.
                return Results.Problem(
                    detail: "A match of this round ended level. A knockout needs a winner: record " +
                            "how the tie was decided before advancing.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            winners.Add(match.HomeTotal > match.AwayTotal ? match.HomeTeamId : match.AwayTeamId);
        }

        // Teams that entered and have never played: the byes of the opening
        // round. A bye leaves no match behind, so this is what it looks like
        // from the outside.
        var played = matches
            .SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId })
            .ToHashSet();

        var byes = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId && team.IsActive)
            .Where(team => !played.Contains(team.Id))
            .OrderBy(team => team.Name)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);

        // Byes first, keeping the order the opening draw gave them.
        List<Guid> advancing = [.. byes, .. winners];

        if (advancing.Count == 1)
        {
            var champion = await database.Teams
                .AsNoTracking()
                .Where(team => team.Id == advancing[0])
                .Select(team => team.Name)
                .SingleOrDefaultAsync(cancellationToken);

            return Results.Ok(new Response(0, round, null, advancing[0], champion));
        }

        var (drawn, _) = Bracket.FirstRound(advancing, round + 1);
        var phase = Bracket.Phase(drawn.Count, round + 1);

        database.Matches.AddRange(drawn.Select(fixture => new Match
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competition.Id,
            CategoryId = categoryId,
            HomeTeamId = fixture.HomeTeamId,
            AwayTeamId = fixture.AwayTeamId,
            RoundNumber = (short)(round + 1),
            Phase = phase,
            Status = MatchState.Scheduled,
        }));

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(drawn.Count, round + 1, phase, null, null));
    }
}
