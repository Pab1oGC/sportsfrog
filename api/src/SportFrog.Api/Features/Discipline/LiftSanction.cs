using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Lifts a sanction on appeal, or undoes that.
/// </summary>
/// <remarks>
/// For an appeal that succeeds — the sanction was correctly raised at the
/// time, but a disciplinary body later overturns it — as opposed to
/// <see cref="VoidSanction"/>, which is for the administrative mistake of
/// having raised it at all. The distinction is why this sits behind a
/// higher bar than voiding does, the same gap
/// <c>Features.Tuition.VoidPayment</c> keeps above
/// <c>Features.Tuition.VoidCharge</c>: reversing a decision that may already
/// have been acted upon — a credential already withheld, a part of the
/// suspension already served — is a bigger step than correcting a slip
/// nothing has happened on yet.
/// </remarks>
public static class LiftSanction
{
    public sealed record Request(string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            // Mirrors ck_sanctions_lift_reason.
            RuleFor(request => request.Reason)
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapLiftSanction(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{competitionId:guid}/sanctions/{id:guid}/lift", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(LiftSanction))
            .WithSummary("Lifts a sanction on appeal, or clears a lift already on it.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        Request request,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var sanction = await database.Sanctions.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId, cancellationToken);

        if (sanction is null)
        {
            return Results.NotFound();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        sanction.LiftedAt = reason is null ? null : clock.GetUtcNow();
        sanction.LiftReason = reason;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
