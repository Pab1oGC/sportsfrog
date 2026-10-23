using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// Asks for a link that lets an account set a new password without knowing
/// the current one (RF-02).
///
/// <see cref="ChangePassword"/> verifies the current password before it
/// changes anything, so it can only ever serve somebody who still knows it.
/// This is the way in for somebody who does not, and the proof it accepts in
/// place of the password is reading the mail sent to the address the account
/// is registered under.
/// </summary>
/// <remarks>
/// Every request is answered 202, whatever happened. An unknown address, a
/// disabled account, an installation with no relay configured and a mail
/// actually on its way are all reported identically — the same rule
/// <see cref="SignIn"/> follows, for the same reason: any difference between
/// them is a way to ask which addresses are registered.
///
/// That rule shapes more than the status code. Nothing is minted or written
/// here, and the mail is sent by <see cref="SendPasswordResetJob"/> after the
/// response has gone out, so an address with an account behind it does not
/// answer more slowly than one without.
/// </remarks>
public static class RequestPasswordReset
{
    public sealed record Request(string Email);

    public static IEndpointRouteBuilder MapRequestPasswordReset(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/password/reset-requests", HandleAsync)
            // Whoever has forgotten their password holds no credentials, and
            // names no organization — this acts on an account, like
            // ChangePassword, and not inside one.
            .AllowAnonymous()
            .WithoutOrganizationContext()
            // There is no validator for this contract, and there must not
            // come to be one: a 400 for a malformed address would mean every
            // 202 came from an address that *was* well formed, which is the
            // very distinction this endpoint exists without. Declared rather
            // than left to the absence of a validator, so writing one later
            // does not quietly turn it on (DD-07).
            .WithoutContractValidation()
            .RequireRateLimiting(RateLimitPolicies.Authentication)
            .WithName(nameof(RequestPasswordReset))
            .WithSummary("Mails a password reset link, if the address has an account.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        HttpContext httpContext,
        SportFrogDbContext database,
        IBackgroundJobClient jobs,
        IOptions<PasswordResetOptions> options,
        CancellationToken cancellationToken)
    {
        if (options.Value.ResetBaseUrl.Length == 0)
        {
            // Recovery is not configured — see PasswordResetOptions for why
            // that is a supported state rather than a startup failure. The
            // caller is told exactly what a configured installation tells
            // them: whether this one has a reset page is not theirs to learn.
            return Accepted();
        }

        var email = request.Email?.Trim() ?? string.Empty;

        // users carries no isolation policy, and the global filter already
        // hides a soft-deleted account. A disabled one is refused here the
        // same way SignIn refuses it: a suspended account is not one to hand
        // a way back in to.
        var userId = string.IsNullOrEmpty(email)
            ? (Guid?)null
            : await database.Users
                .Where(candidate => candidate.Email == email && candidate.IsActive)
                .Select(candidate => (Guid?)candidate.Id)
                .SingleOrDefaultAsync(cancellationToken);

        // Queued whether or not an account was found. The job does nothing
        // for an address nobody has registered, and that is the point:
        // enqueueing is a database write, and a write that happened only for
        // addresses with an account behind them is a difference in response
        // time that can be measured — it was, before this was unconditional,
        // at seconds rather than milliseconds whenever the queue's storage
        // was slow to answer.
        Queue(httpContext, jobs, userId ?? Guid.Empty);

        return Accepted();
    }

    /// <summary>
    /// Queues the mail once the response is already written.
    /// </summary>
    /// <remarks>
    /// The same placement <c>ImportAthletePhotos.Queue</c> uses, though for a
    /// different reason: there it is about ordering against a commit, here it
    /// is about keeping the queue's own latency off the caller's clock. What
    /// the uniform timing actually rests on is the call being unconditional —
    /// see the call site.
    ///
    /// <see cref="Guid.Empty"/> is the "no account" case, which
    /// <see cref="SendPasswordResetJob"/> recognizes and drops.
    /// </remarks>
    private static void Queue(HttpContext context, IBackgroundJobClient jobs, Guid userId) =>
        context.Response.OnCompleted(() =>
        {
            jobs.Enqueue<SendPasswordResetJob>(job =>
                job.RunAsync(userId, CancellationToken.None));

            return Task.CompletedTask;
        });

    /// <summary>
    /// One answer for every outcome. Accepted, not NoContent: what was
    /// accepted is the request, and whether anything came of it is
    /// deliberately not said.
    /// </summary>
    private static IResult Accepted() => Results.Accepted();
}
