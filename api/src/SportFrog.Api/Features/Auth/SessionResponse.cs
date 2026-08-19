using SportFrog.Api.Infrastructure.Auth;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// What signing in and renewing both return. One shape, because a client
/// should not have to tell the two apart: the second is how the first is kept
/// alive.
/// </summary>
/// <param name="AccessToken">Short-lived. Sent as a bearer token.</param>
/// <param name="RefreshToken">Revocable, and returned exactly once.</param>
/// <param name="ExpiresIn">Seconds the access token remains valid.</param>
/// <param name="Organizations">Where the account may act, and as what.</param>
public sealed record SessionResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    IReadOnlyCollection<SessionResponse.OrganizationSummary> Organizations)
{
    /// <param name="Id">Value for the organization header on later requests.</param>
    public sealed record OrganizationSummary(Guid Id, string Name, string Slug, string Role);

    public static SessionResponse From(SessionIssuer.Session session) => new(
        session.AccessToken,
        session.RefreshToken,
        session.ExpiresInSeconds,
        [.. session.Organizations.Select(organization => new OrganizationSummary(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Role.ToString().ToLowerInvariant()))]);
}
