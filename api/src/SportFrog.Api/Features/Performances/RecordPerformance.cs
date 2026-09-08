using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Records what a team scored in a classification stage, or corrects what
/// was recorded.
/// </summary>
/// <remarks>
/// The judged-score sibling of <see cref="Matches.RecordResult"/>, and much
/// simpler than it: a performance has one side and one number, so there is
/// no consolidation to run through <c>IMatchOutcomeRules</c> and no shape to
/// check through <c>IResultShapeRules</c> — the only thing a score can get
/// wrong here is being negative.
/// </remarks>
public static class RecordPerformance
{
    /// <param name="Score">
    /// The judges' score, ×100 — 7.65 points is sent as 765. See
    /// <c>PeriodScore</c>'s remarks on why.
    /// </param>
    public sealed record Request(int Score, string? Notes);

    public sealed record Response(int Score);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Score)
                .GreaterThanOrEqualTo(0).WithMessage("El puntaje no puede ser negativo.");

            RuleFor(request => request.Notes)
                .MaximumLength(1000)
                .When(request => request.Notes is not null);
        }
    }

    public static IEndpointRouteBuilder MapRecordPerformance(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/performances/{id:guid}/score", HandleAsync)
            // The role exists for this: recording a judged score is the
            // narrowest job in the organization, same as recording a match.
            .RequireRole(MembershipRole.Recorder)
            .WithName(nameof(RecordPerformance))
            .WithSummary("Records a team's judged score for a classification-stage performance.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var performance = await database.Performances
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (performance?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            return Results.Problem(
                detail: "Esta competencia ya terminó, así que su clasificación está cerrada.",
                statusCode: StatusCodes.Status409Conflict);
        }

        performance.Score = request.Score;
        performance.Status = PerformanceStatus.Scored;
        performance.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Who first recorded it and who changed it afterwards are different
        // questions, same as a match's result.
        var now = clock.GetUtcNow();

        if (performance.RecordedBy is null)
        {
            performance.RecordedBy = organization.UserId;
            performance.RecordedAt = now;
        }
        else
        {
            performance.ModifiedBy = organization.UserId;
            performance.ModifiedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(performance.Score.Value));
    }
}
