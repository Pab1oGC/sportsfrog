using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws the fixtures of a category.
/// </summary>
/// <remarks>
/// The point of the feature: a twenty-team league is a hundred and ninety
/// fixtures, and entering those by hand is where an organizer gives up on the
/// product.
///
/// The draw produces pairings and rounds and nothing else — no dates, no
/// pitches. Those are a separate question with separate constraints, and
/// conflating them means a calendar cannot be redrawn without also
/// rescheduling everything that was already agreed with the venues.
/// </remarks>
public static class DrawCalendar
{
    /// <param name="Legs">
    /// One round of matches, or home and away.
    /// </param>
    public sealed record Request(int Legs);

    /// <param name="Replaced">
    /// How many fixtures the draw removed to make room. A redraw is ordinary
    /// — the first one is rarely the one that runs — so it is reported rather
    /// than refused.
    /// </param>
    public sealed record Response(int Created, int Replaced, int Rounds);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Legs)
                .InclusiveBetween(1, 2)
                .WithMessage("A league is drawn in one leg, or two for home and away.");
    }

    public static IEndpointRouteBuilder MapDrawCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/draw", HandleAsync)
            // Drawing the calendar is running the competition, the same job
            // that enters the clubs and places the fixtures by hand.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DrawCalendar))
            .WithSummary("Draws an all-play-all calendar for a category.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
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

        if (competition.Format != CompetitionFormat.League)
        {
            // This draw is the all-play-all one. A knockout bracket and a
            // group stage are different shapes, not different parameters of
            // this one.
            return Results.Problem(
                detail: $"This draw is for the league format. This competition is drawn as " +
                        $"{competition.Format}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            return Results.Problem(
                detail: "This competition is already under way, so its calendar cannot be drawn " +
                        "again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Only teams still competing. One that withdrew keeps the matches it
        // played and is not given new ones, which is the same rule that stops
        // it being scheduled by hand.
        var teams = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId && team.IsActive)
            .OrderBy(team => team.Name)
            .Select(team => team.Id)
            .ToListAsync(cancellationToken);

        if (teams.Count < 2)
        {
            return Results.Problem(
                detail: "A calendar needs at least two teams still competing in the category.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var existing = await database.Matches
            .Where(match => match.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        if (existing.Any(match =>
                match.Status is MatchState.Finished or MatchState.Walkover))
        {
            // Something has been played under the current calendar. Redrawing
            // would leave a result attached to a fixture the new draw does not
            // contain, which is a worse state than any calendar.
            return Results.Problem(
                detail: "Results have already been recorded in this category, so its calendar " +
                        "cannot be redrawn. Remove the fixtures that have not been played and " +
                        "add the missing ones by hand.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = clock.GetUtcNow();

        foreach (var match in existing)
        {
            // Struck rather than removed: fixtures are deleted logically
            // everywhere else, and a redraw is the ordinary case rather than
            // a correction of one.
            match.DeletedAt = now;
        }

        var drawn = RoundRobin.Draw(teams, request.Legs);

        database.Matches.AddRange(drawn.Select(fixture => new Match
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competition.Id,
            CategoryId = categoryId,
            HomeTeamId = fixture.HomeTeamId,
            AwayTeamId = fixture.AwayTeamId,
            RoundNumber = (short)fixture.Round,
            Status = MatchState.Scheduled,
        }));

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            drawn.Count,
            existing.Count,
            drawn.Count == 0 ? 0 : drawn.Max(fixture => fixture.Round)));
    }
}
