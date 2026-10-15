using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Sessions;

/// <summary>
/// Cancels one dated class, or reinstates it.
/// </summary>
/// <remarks>
/// A toggle, the same shape as <see cref="Rosters.WithdrawPlayer"/>:
/// reversible, because a cancellation is a claim about the world (rain,
/// the venue fell through) and claims are sometimes entered wrongly.
///
/// Refused once the session is <see cref="CourseSessionState.Held"/>: a
/// class that already ran did not get rained out, and cancelling it after
/// the fact would contradict whatever was recorded against it.
/// </remarks>
public static class CancelSession
{
    public sealed record Request(bool Cancelled, string? Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Reason)
                .NotEmpty().WithMessage("Hay que decir por qué se cancela.")
                .MaximumLength(300)
                .When(request => request.Cancelled);
    }

    public static IEndpointRouteBuilder MapCancelSession(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/sessions/{id:guid}/cancellation", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseSession, "id"))
            .WithName(nameof(CancelSession))
            .WithSummary("Cancels a session, or reinstates it.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var session = await database.CourseSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (session is null)
        {
            return Results.NotFound();
        }

        var alreadyCancelled = session.Status == CourseSessionState.Cancelled;

        if (request.Cancelled == alreadyCancelled)
        {
            // Already where the caller is asking for. A second click on
            // "cancelar" is not a mistake to report.
            return Results.NoContent();
        }

        if (request.Cancelled)
        {
            if (session.Status == CourseSessionState.Held)
            {
                return Results.Problem(
                    detail: "Esta clase ya se dictó, así que no se puede cancelar.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            session.Status = CourseSessionState.Cancelled;

            // Validated NotEmpty above when Cancelled is true.
            session.CancellationReason = request.Reason!.Trim();
        }
        else
        {
            session.Status = CourseSessionState.Scheduled;
            session.CancellationReason = null;
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
