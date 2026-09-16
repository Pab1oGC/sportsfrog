using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Sets one team's mat, day and turn in a classification stage's running
/// order — or clears it, moving the team back to "not yet scheduled".
/// </summary>
/// <remarks>
/// The performance row itself already exists from the moment
/// <see cref="OpenClassificationStage"/> ran — there is nothing to create
/// here, only where and when it happens to move to, the running-order sibling
/// of <see cref="Matches.RescheduleMatch"/>.
/// </remarks>
public static class SchedulePerformance
{
    public sealed record Request(Guid? VenueSpaceId, DateOnly? ScheduledOn, short? OrderNumber);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.OrderNumber)
                .InclusiveBetween((short)1, (short)999)
                .When(request => request.OrderNumber.HasValue)
                .WithMessage("El turno está entre 1 y 999.");
    }

    public static IEndpointRouteBuilder MapSchedulePerformance(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/performances/{id:guid}/schedule", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(SchedulePerformance))
            .WithSummary("Sets a performance's mat, day and turn in the running order.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        PerformancePolicy policy,
        OrganizationContext organization,
        IBackgroundJobClient jobs,
        CancellationToken cancellationToken)
    {
        var performance = await database.Performances.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (performance is null)
        {
            return Results.NotFound();
        }

        var violations = await policy.InspectAsync(
            request.VenueSpaceId, request.ScheduledOn, request.OrderNumber,
            excludingPerformanceId: id, cancellationToken);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        // Whether this actually moves the slot — the only thing worth
        // writing to a club about.
        var slotChanged = performance.VenueSpaceId != request.VenueSpaceId
            || performance.ScheduledOn != request.ScheduledOn
            || performance.OrderNumber != request.OrderNumber;

        performance.VenueSpaceId = request.VenueSpaceId;
        performance.ScheduledOn = request.ScheduledOn;
        performance.OrderNumber = request.OrderNumber;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Ese turno se ocupó justo ahora, entre que se revisó y que se guardó. " +
                        "Volvé a intentar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (slotChanged)
        {
            Notify(context, jobs, organization.RequireOrganizationId(), organization.UserId ?? Guid.Empty, id);
        }

        return Results.NoContent();
    }

    /// <summary>
    /// Queued once the response has actually gone out — see
    /// <see cref="Matches.RescheduleMatch"/>'s own remarks on why.
    /// </summary>
    private static void Notify(
        HttpContext context, IBackgroundJobClient jobs, Guid organizationId, Guid userId, Guid performanceId) =>
        context.Response.OnCompleted(() =>
        {
            jobs.Enqueue<PerformanceRescheduleNotificationJob>(
                job => job.RunAsync(organizationId, userId, performanceId, CancellationToken.None));

            return Task.CompletedTask;
        });
}
