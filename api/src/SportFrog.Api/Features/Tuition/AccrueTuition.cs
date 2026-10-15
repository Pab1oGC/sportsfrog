using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Raises one month's pension for the active organization, by hand.
/// </summary>
/// <remarks>
/// The same work <see cref="TuitionAccrualJob"/> runs once a month for
/// every organization on its own — this is here so a forgotten month can
/// be closed today rather than waiting for the schedule, and so the whole
/// feature works before a single recurring job has ever fired.
/// </remarks>
public static class AccrueTuition
{
    public sealed record Request(int Year, int Month);

    public sealed record Response(int Charged, int AlreadyCharged, DateOnly Period);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Month)
                .InclusiveBetween(1, 12)
                .WithMessage("El mes tiene que estar entre 1 y 12.");

            // A ceiling to catch a mistyped year, not a real limit.
            RuleFor(request => request.Year)
                .InclusiveBetween(2020, 2100)
                .WithMessage("Ese año no es posible.");
        }
    }

    public static IEndpointRouteBuilder MapAccrueTuition(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/tuition/accruals", HandleAsync)
            // The same tier Course itself is configured at: raising a
            // pension is a financial decision for the organization, not an
            // operation any one course's own staff reaches for.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(AccrueTuition))
            .WithSummary("Raises one month's pension for every enrollment that owes it.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Request request,
        TuitionAccrual accrual,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var period = new DateOnly(request.Year, request.Month, 1);
        var currentPeriod = new DateOnly(clock.GetUtcNow().Year, clock.GetUtcNow().Month, 1);

        if (period > currentPeriod)
        {
            // A month that has not started yet has no enrollments it could
            // honestly be said to owe — whoever is enrolled by then is not
            // settled.
            return Results.Problem(
                detail: "No se puede devengar un período que todavía no llegó.",
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            var result = await accrual.RunAsync(organization.RequireOrganizationId(), period, cancellationToken);

            return Results.Ok(new Response(result.Charged, result.AlreadyCharged, result.Period));
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two accruals for the same organization and period at once —
            // this call racing the monthly schedule, most likely. Nothing
            // here answers which rows made it in before the collision, so
            // the honest answer is to ask for a clean retry: the next run
            // only inserts what is still missing either way.
            return Results.Problem(
                detail: "Alguien más devengó este período en el mismo instante. Volvé a intentarlo.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
