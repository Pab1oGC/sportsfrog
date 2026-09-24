using FluentValidation;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.MatchEvents;

/// <summary>
/// Records something a player did in a match.
/// </summary>
public static class RecordEvent
{
    public sealed record Request(
        Guid RosterEntryId,
        Guid MetricId,
        short? PeriodNumber,
        short? Minute,
        int Quantity);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        /// <summary>
        /// Four hours of play, which no sport in the catalog reaches and a
        /// mistyped minute passes.
        /// </summary>
        private const short MaximumMinute = 240;

        /// <summary>
        /// A hundred of the same thing in one match is not a tally, it is a
        /// slipped digit.
        /// </summary>
        private const int MaximumQuantity = 100;

        public Validator()
        {
            RuleFor(request => request.RosterEntryId)
                .NotEmpty().WithMessage("El jugador es obligatorio.");

            RuleFor(request => request.MetricId)
                .NotEmpty().WithMessage("El evento es obligatorio.");

            RuleFor(request => request.Minute)
                .InclusiveBetween((short)0, MaximumMinute)
                .When(request => request.Minute.HasValue)
                .WithMessage($"El minuto está entre 0 y {MaximumMinute}, o se deja sin definir.");

            // Mirrors the schema's CHECK (quantity > 0). Stated here as well
            // so a zero is answered by naming the field rather than by a
            // constraint name.
            RuleFor(request => request.Quantity)
                .InclusiveBetween(1, MaximumQuantity)
                .WithMessage($"La cantidad está entre 1 y {MaximumQuantity}.");
        }
    }

    public static IEndpointRouteBuilder MapRecordEvent(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/matches/{matchId:guid}/events", HandleAsync)
            // The recorder's job, like the score: this is what somebody at the
            // side of the pitch is doing on their phone.
            .RequireRole(MembershipRole.Recorder)
            .WithName(nameof(RecordEvent))
            .WithSummary("Records a player event in a match.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid matchId,
        Request request,
        SportFrogDbContext database,
        EventPolicy policy,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await policy.FindContextAsync(matchId, cancellationToken) is not { } context)
        {
            return Results.NotFound();
        }

        if (EventPolicy.RefuseMatch(context) is { } refusal)
        {
            return refusal;
        }

        var violations = await policy.InspectAsync(
            context, request.RosterEntryId, request.MetricId, request.PeriodNumber,
            request.Minute, request.Quantity, cancellationToken);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        var recorded = new PlayerEvent
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            MatchId = matchId,
            RosterEntryId = request.RosterEntryId,
            MetricId = request.MetricId,
            PeriodNumber = request.PeriodNumber,
            Minute = request.Minute,
            Quantity = request.Quantity,
        };

        database.PlayerEvents.Add(recorded);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created($"/events/{recorded.Id}", new Response(recorded.Id));
    }
}
