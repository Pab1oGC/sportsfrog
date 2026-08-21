using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Moves a fixture: another time, another pitch, another round.
/// </summary>
/// <remarks>
/// The teams can be changed too, and only while nothing has been played. A
/// draw made wrongly is corrected by fixing it; a match that has a result is
/// a record of what two particular sides did, and pointing it at a third
/// would move that result onto a team that never played it.
///
/// Nothing here touches the score or the state — those are recorded, not
/// edited into place.
/// </remarks>
public static class RescheduleMatch
{
    public sealed record Request(
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid? VenueSpaceId,
        DateTimeOffset? ScheduledAt,
        short? RoundNumber,
        string? Phase,
        string? Notes);

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

    public static IEndpointRouteBuilder MapRescheduleMatch(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/matches/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RescheduleMatch))
            .WithSummary("Moves a fixture.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        FixturePolicy policy,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (match is null)
        {
            return Results.NotFound();
        }

        var teamsChanged =
            request.HomeTeamId != match.HomeTeamId || request.AwayTeamId != match.AwayTeamId;

        if (teamsChanged && match.Status is not (MatchState.Scheduled or MatchState.Postponed))
        {
            return Results.Problem(
                detail: "This match has already been played or awarded, so who played it is " +
                        "fixed. Only a fixture still waiting to be played can change sides.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var violations = await policy.InspectAsync(
            match.CategoryId, request.HomeTeamId, request.AwayTeamId, request.VenueSpaceId,
            cancellationToken);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        match.HomeTeamId = request.HomeTeamId;
        match.AwayTeamId = request.AwayTeamId;
        match.VenueSpaceId = request.VenueSpaceId;
        match.ScheduledAt = request.ScheduledAt;
        match.RoundNumber = request.RoundNumber;
        match.Phase = string.IsNullOrWhiteSpace(request.Phase) ? null : request.Phase.Trim();
        match.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "That space is already taken at that time. Two matches cannot share a " +
                        "pitch, so move one of them.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
