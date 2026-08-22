using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// What every anonymous endpoint has in common.
/// </summary>
/// <remarks>
/// Three things have to be true of all of them, and stating each one at each
/// endpoint is how one of them eventually goes missing.
///
/// No credentials, because there is nobody signed in. No organization
/// context from the entry channel, because the address carries slugs rather
/// than an identifier and the context is established by resolving them. And a
/// rate limit, because this is the only part of the API facing the open
/// internet.
/// </remarks>
internal static class PublicRoutes
{
    /// <summary>The address every public reading hangs off.</summary>
    public const string Prefix = "/public/{organizationSlug}/{competitionSlug}";

    public static RouteHandlerBuilder AsPublicReading(this RouteHandlerBuilder builder) =>
        builder
            .AllowAnonymous()
            .WithoutOrganizationContext()
            .RequireRateLimiting(RateLimitPolicies.Public);
}
