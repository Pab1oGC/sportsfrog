using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Puts a fixture on the calendar.
/// </summary>
public static class ScheduleMatch
{
    public sealed record Request(
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid? VenueSpaceId,
        DateTimeOffset? ScheduledAt,
        short? RoundNumber,
        string? Phase,
        string? Notes);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.HomeTeamId)
                .NotEmpty().WithMessage("The home team is required.");

            RuleFor(request => request.AwayTeamId)
                .NotEmpty().WithMessage("The away team is required.");

            RuleFor(request => request.RoundNumber)
                .InclusiveBetween((short)1, (short)200)
                .When(request => request.RoundNumber.HasValue)
                .WithMessage("A round number is between 1 and 200.");

            RuleFor(request => request.Phase)
                .MaximumLength(40)
                .When(request => request.Phase is not null);

            RuleFor(request => request.Notes)
                .MaximumLength(1000)
                .When(request => request.Notes is not null);
        }
    }

    public static IEndpointRouteBuilder MapScheduleMatch(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/matches", HandleAsync)
            // Drawing the calendar is running the competition, which is the
            // operator's work — the same role that enters the clubs.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(ScheduleMatch))
            .WithSummary("Puts a fixture on the calendar.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        FixturePolicy policy,
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

        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            // Adding a fixture to a competition that is over does not schedule
            // anything; it edits history. A draft is left open because a
            // calendar is often built before the competition is announced.
            return Results.Problem(
                detail: "This competition is over, so its calendar is closed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var violations = await policy.InspectAsync(
            categoryId, request.HomeTeamId, request.AwayTeamId, request.VenueSpaceId,
            cancellationToken);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        var match = new Match
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competition.Id,
            CategoryId = categoryId,
            HomeTeamId = request.HomeTeamId,
            AwayTeamId = request.AwayTeamId,
            VenueSpaceId = request.VenueSpaceId,
            ScheduledAt = request.ScheduledAt,
            RoundNumber = request.RoundNumber,
            Phase = string.IsNullOrWhiteSpace(request.Phase) ? null : request.Phase.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = MatchState.Scheduled,
        };

        database.Matches.Add(match);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // uq_space_schedule: one live fixture per pitch per moment. The
            // rule lives in the schema, which is the right place for it —
            // nothing here could enforce it against two requests arriving
            // together — so this only has to say it in words.
            return Results.Problem(
                detail: "That space is already taken at that time. Two matches cannot share a " +
                        "pitch, so move one of them.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/matches/{match.Id}", new Response(match.Id));
    }
}
