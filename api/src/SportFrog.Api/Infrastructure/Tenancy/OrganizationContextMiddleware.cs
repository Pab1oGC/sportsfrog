using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// Establishes the organization context for the request, and the transaction
/// it lives in.
///
/// This is the application half of the two-layer isolation the design calls
/// its most critical requirement (RNF-11, DD-02). The database half — the
/// row-level policies — is already in place and would return nothing without
/// this; together they mean a programming mistake cannot leak data between
/// clients.
///
/// It runs at the entry channel rather than being invoked by each feature
/// (DD-07): a guarantee that depends on every author remembering it is not a
/// guarantee. The default is to require a context, so a new endpoint is
/// covered the moment it exists.
/// </summary>
public sealed class OrganizationContextMiddleware(
    RequestDelegate next,
    ILogger<OrganizationContextMiddleware> logger)
{
    /// <summary>
    /// Header naming the organization the caller is acting in. The token says
    /// which organizations they may act in; this says which one they mean
    /// right now. It is a header and not a path segment because the
    /// administrative routes are not scoped by organization in their address.
    /// </summary>
    public const string OrganizationHeader = "X-Organization-Id";

    public async Task InvokeAsync(
        HttpContext context,
        SportFrogDbContext database,
        OrganizationContext organizationContext)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint is null)
        {
            // No route matched. Nothing downstream will reach the database,
            // so demanding a context here would only turn a mistyped address
            // into a misleading authentication failure.
            await next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<WithoutOrganizationContextAttribute>() is not null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            // No credentials at all. Answered as an authentication failure,
            // not as a missing header, so the caller is told what to fix.
            await RefuseAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                "no credentials were presented");
            return;
        }

        if (!TryReadOrganization(context, out var organizationId))
        {
            // A missing header is a contract error, not a security detail:
            // saying so plainly is what lets a client fix its request instead
            // of guessing.
            await RefuseAsync(
                context,
                StatusCodes.Status400BadRequest,
                $"The {OrganizationHeader} header is required and must be an organization identifier.",
                "the organization header was missing or malformed");
            return;
        }

        if (!TryReadUser(context, out var userId))
        {
            // The token validated but names no usable subject. Nothing can be
            // attributed to it, and the audit log would have no author to
            // record, so it is refused rather than run anonymously.
            await RefuseAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                "the token carries no usable subject");
            return;
        }

        var role = JwtAccessTokenIssuer.FindRole(context.User, organizationId);
        if (role is null)
        {
            // The token grants nothing in that organization. Refused without
            // looking the organization up, so the answer is the same whether
            // it exists or not and reveals neither — and the message says
            // nothing beyond the refusal itself.
            await RefuseAsync(
                context,
                StatusCodes.Status403Forbidden,
                "This operation is not allowed.",
                "the token grants no role in organization {OrganizationId}",
                organizationId);
            return;
        }

        organizationContext.Establish(
            organizationId,
            role.Value,
            userId,
            context.Connection.RemoteIpAddress);

        // Structured logging carries the organization, without which a trace
        // cannot be read in a multi-organization system (section 8).
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["OrganizationId"] = organizationId,
            ["Role"] = role.Value,
        });

        await RunInOrganizationTransactionAsync(context, database, organizationId);
    }

    /// <summary>
    /// Runs the rest of the pipeline inside one transaction whose isolation
    /// context is set for that transaction only.
    /// </summary>
    /// <remarks>
    /// Transaction scope is not an implementation detail: connections are
    /// reused from a shared pool, so a value scoped to the connection would
    /// still be there for whoever borrows it next. That is the leak this
    /// design exists to prevent.
    /// </remarks>
    private async Task RunInOrganizationTransactionAsync(
        HttpContext context,
        SportFrogDbContext database,
        Guid organizationId)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            context.RequestAborted);

        // set_config with is_local = true is SET LOCAL, and unlike the
        // statement it can be parameterized.
        await database.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_org', {0}, true)",
            [organizationId.ToString()],
            context.RequestAborted);

        try
        {
            await next(context);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            // A refusal must not leave half of its work behind.
            await transaction.RollbackAsync(CancellationToken.None);
            return;
        }

        await transaction.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// Refuses the request with a body the caller can act on, and one log
    /// line for whoever has to diagnose it.
    /// </summary>
    /// <remarks>
    /// The two audiences get different detail on purpose. The client is told
    /// what to fix when that is a contract error, and nothing beyond the
    /// refusal when it is an authorization one. The reason — always specific
    /// — stays in the internal log.
    /// </remarks>
    private async Task RefuseAsync(
        HttpContext context,
        int statusCode,
        string clientDetail,
        string internalReason,
        params object?[] reasonArguments)
    {
#pragma warning disable CA2254 // The template is one of a fixed set defined above, not user input.
        logger.LogWarning(
            "Refused {Method} {Path} with {StatusCode}: " + internalReason,
            [context.Request.Method, context.Request.Path.Value, statusCode, .. reasonArguments]);
#pragma warning restore CA2254

        await Results
            .Problem(detail: clientDetail, statusCode: statusCode)
            .ExecuteAsync(context);
    }

    /// <summary>
    /// The person the validated token names.
    /// </summary>
    private static bool TryReadUser(HttpContext context, out Guid userId) =>
        Guid.TryParse(
            context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out userId);

    private static bool TryReadOrganization(HttpContext context, out Guid organizationId)
    {
        organizationId = Guid.Empty;

        var value = context.Request.Headers[OrganizationHeader].ToString();

        return !string.IsNullOrWhiteSpace(value)
            && Guid.TryParse(value, out organizationId)
            && organizationId != Guid.Empty;
    }
}
