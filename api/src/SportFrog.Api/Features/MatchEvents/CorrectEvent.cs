using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.MatchEvents;

/// <summary>
/// Corrects when an event happened, or how many.
/// </summary>
/// <remarks>
/// Neither the player nor the metric can be changed. An event credited to the
/// wrong person or entered as the wrong thing is not a detail to adjust — it
/// is a goal that somebody else scored, or a card that was really an assist.
/// Remove it and record the right one, so the mistake leaves no half-corrected
/// row behind claiming to be the original.
/// </remarks>
public static class CorrectEvent
{
    public sealed record Request(short? PeriodNumber, short? Minute, int Quantity);

    internal sealed class Validator : AbstractValidator<Request>
    {
        private const short MaximumMinute = 240;
        private const int MaximumQuantity = 100;

        public Validator()
        {
            RuleFor(request => request.Minute)
                .InclusiveBetween((short)0, MaximumMinute)
                .When(request => request.Minute.HasValue)
                .WithMessage($"A minute is between 0 and {MaximumMinute}, or is left unset.");

            RuleFor(request => request.Quantity)
                .InclusiveBetween(1, MaximumQuantity)
                .WithMessage($"A quantity is between 1 and {MaximumQuantity}.");
        }
    }

    public static IEndpointRouteBuilder MapCorrectEvent(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/events/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Recorder)
            .WithName(nameof(CorrectEvent))
            .WithSummary("Corrects the minute or quantity of an event.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        EventPolicy policy,
        CancellationToken cancellationToken)
    {
        var recorded = await database.PlayerEvents.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (recorded is null)
        {
            return Results.NotFound();
        }

        if (await policy.FindContextAsync(recorded.MatchId, cancellationToken) is not { } context)
        {
            return Results.NotFound();
        }

        // Only the period is worth re-checking: the player and the metric are
        // not moving, and they were judged when the event was recorded.
        var violations = await policy.InspectAsync(
            context, recorded.RosterEntryId, recorded.MetricId, request.PeriodNumber,
            cancellationToken);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        recorded.PeriodNumber = request.PeriodNumber;
        recorded.Minute = request.Minute;
        recorded.Quantity = request.Quantity;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
