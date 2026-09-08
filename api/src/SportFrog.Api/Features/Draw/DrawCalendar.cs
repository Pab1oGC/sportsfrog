using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Draws the fixtures of a category, in the shape its competition is played.
/// </summary>
/// <remarks>
/// The point of the feature: a twenty-team league is a hundred and ninety
/// fixtures, and entering those by hand is where an organizer gives up on the
/// product.
///
/// Three shapes, and they are genuinely different rather than parameters of
/// one another. A league draws everything at once. A group stage is a league
/// per group, drawn together so the rounds line up across them. A knockout can
/// only draw its first round, because who plays the second is not known until
/// the first is played.
///
/// None of them produce dates or pitches. That is a separate question with
/// separate constraints, and conflating them means a calendar cannot be
/// redrawn without also rescheduling everything already agreed with the
/// venues.
/// </remarks>
public static class DrawCalendar
{
    /// <param name="Legs">
    /// One round of matches, or home and away. Leagues and group stages only:
    /// a two-legged knockout tie is a different object, decided on aggregate
    /// rather than on one result.
    /// </param>
    public sealed record Request(int Legs);

    /// <param name="Replaced">
    /// How many fixtures the draw removed to make room. A redraw is ordinary
    /// — the first one is rarely the one that runs — so it is reported rather
    /// than refused.
    /// </param>
    /// <param name="Byes">
    /// Teams that advance without playing, when a knockout entry list is not
    /// a power of two.
    /// </param>
    public sealed record Response(
        int Created,
        int Replaced,
        int Rounds,
        string? Phase = null,
        int Byes = 0);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Legs)
                .InclusiveBetween(1, 2)
                .WithMessage("Un sorteo corre a una vuelta, o dos para ida y vuelta.");
    }

    public static IEndpointRouteBuilder MapDrawCalendar(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/draw", HandleAsync)
            // Drawing the calendar is running the competition, the same job
            // that enters the clubs and places the fixtures by hand.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DrawCalendar))
            .WithSummary("Draws the fixtures of a category.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        ICalendarDrawRegistry draws,
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

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            return Results.Problem(
                detail: "Esta competencia ya está en curso, así que su calendario no se puede " +
                        "volver a sortear.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var draw = draws.For(competition.Format);

        // Only teams still competing. One that withdrew keeps the matches it
        // played and is not given new ones, which is the same rule that stops
        // it being scheduled by hand.
        var teams = await database.Teams
            .AsNoTracking()
            .Where(team => team.CategoryId == categoryId && team.IsActive)
            .OrderBy(team => team.Name)
            .Select(team => new DrawnTeam(team.Id, team.GroupLabel))
            .ToListAsync(cancellationToken);

        if (teams.Count < 2)
        {
            return Results.Problem(
                detail: "Un calendario necesita al menos dos equipos que sigan compitiendo en la categoría.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (draw.Inspect(teams, request.Legs) is { } problem)
        {
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status409Conflict);
        }

        var existing = await database.Matches
            .Where(match => match.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        if (existing.Any(match => match.Status is MatchState.Finished or MatchState.Walkover))
        {
            // Something has been played under the current calendar. Redrawing
            // would leave a result attached to a fixture the new draw does not
            // contain, which is a worse state than any calendar.
            return Results.Problem(
                detail: "Ya se registraron resultados en esta categoría, así que su calendario " +
                        "no se puede volver a sortear. Eliminá los partidos que no se jugaron y " +
                        "agregá los que falten a mano.",
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

        var (drawn, phase, byes) = draw.Draw(teams, request.Legs);

        database.Matches.AddRange(drawn.Select(fixture => new Match
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competition.Id,
            CategoryId = categoryId,
            HomeTeamId = fixture.HomeTeamId,
            AwayTeamId = fixture.AwayTeamId,
            RoundNumber = (short)fixture.Round,
            Phase = phase,
            Status = MatchState.Scheduled,
        }));

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            drawn.Count,
            existing.Count,
            drawn.Count == 0 ? 0 : drawn.Max(fixture => fixture.Round),
            phase,
            byes));
    }
}
