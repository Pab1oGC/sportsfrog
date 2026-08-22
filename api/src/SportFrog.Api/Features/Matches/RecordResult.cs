using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Records what a match finished, or corrects what was recorded.
/// </summary>
/// <remarks>
/// The score is what makes a match finished, which is why this is not a
/// status change with a body attached. <c>ck_result_completeness</c> refuses a
/// finished match without totals and an author, so the two arrive together or
/// not at all.
///
/// Only the periods are sent. The match score is computed from them according
/// to the sport, because a caller that could state both could state a football
/// match of 1-0 and 2-1 that finished 5-0, and nothing would notice.
/// </remarks>
public static class RecordResult
{
    public sealed record Request(IReadOnlyList<PeriodScore> PeriodScores, string? Notes);

    /// <param name="HomeTotal">
    /// The match score, consolidated. Returned because it is derived rather
    /// than sent, and the caller has no other way to learn what their periods
    /// added up to.
    /// </param>
    public sealed record Response(int HomeTotal, int AwayTotal);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.PeriodScores)
                .NotEmpty().WithMessage("The score of each period is required.");

            RuleFor(request => request.Notes)
                .MaximumLength(1000)
                .When(request => request.Notes is not null);
        }
    }

    public static IEndpointRouteBuilder MapRecordResult(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/matches/{id:guid}/result", HandleAsync)
            // The role exists for this. Recording what happened on the field
            // is the narrowest job in the organization, and it should not
            // require the authority to redraw the calendar.
            .RequireRole(MembershipRole.Recorder)
            .WithName(nameof(RecordResult))
            .WithSummary("Records the result of a match.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        ResultPolicy policy,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (match is null)
        {
            return Results.NotFound();
        }

        if (match.Status is MatchState.Cancelled or MatchState.Walkover)
        {
            // A cancelled match produced nothing, and a walkover was awarded
            // without being played. Giving either a score would contradict the
            // reason it holds the state it does.
            return Results.Problem(
                detail: match.Status == MatchState.Cancelled
                    ? "This match was cancelled, so it has no result. Put it back on the " +
                      "calendar first if it is going to be played."
                    : "This match was awarded as a walkover. Nothing was played, so there is no " +
                      "score to record.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await policy.FindRulesAsync(match, cancellationToken) is not { } rules)
        {
            return Results.Problem(
                detail: "The rules for this match cannot be read, so its result cannot be judged.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var violations = ResultPolicy.Inspect(rules, request.PeriodScores);

        if (violations.Count > 0)
        {
            return Results.ValidationProblem(violations
                .GroupBy(violation => violation.Property)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(violation => violation.Message).ToArray()));
        }

        var (home, away) = ScoreConsolidation.Consolidate(
            rules.Sport.ScoreMode, request.PeriodScores);

        match.PeriodScores = [.. request.PeriodScores.OrderBy(period => period.Period)];
        match.HomeTotal = home;
        match.AwayTotal = away;
        match.Status = MatchState.Finished;

        if (request.Notes is not null)
        {
            match.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        }

        // Who recorded it and who changed it afterwards are different
        // questions, and a competition asks the second one out loud when a
        // score is disputed. The first author is never overwritten.
        var now = clock.GetUtcNow();

        if (match.RecordedBy is null)
        {
            match.RecordedBy = organization.UserId;
            match.RecordedAt = now;
        }
        else
        {
            match.ModifiedBy = organization.UserId;
            match.ModifiedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(home, away));
    }
}
