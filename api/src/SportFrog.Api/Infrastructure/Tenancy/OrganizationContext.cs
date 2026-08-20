using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// The organization the current request is acting in, and the role the caller
/// holds there. Scoped to one request.
///
/// Features read this instead of re-deriving the organization from the token:
/// there is one answer per request, established once at the entry channel
/// (DD-07), and everything downstream agrees with the isolation context the
/// database is enforcing for the same transaction.
/// </summary>
public sealed class OrganizationContext
{
    public Guid? OrganizationId { get; private set; }

    /// <summary>
    /// The person the request acts as. The audit log needs it to name an
    /// author, which is the one thing a change record cannot be reconstructed
    /// without.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>Where the request came from, recorded alongside a change.</summary>
    public System.Net.IPAddress? IpAddress { get; private set; }

    public MembershipRole? Role { get; private set; }

    /// <summary>
    /// False on the paths that legitimately run without one — signing in,
    /// registering an organization, the health probe. On every other path the
    /// entry channel has already refused the request before a feature could
    /// observe this.
    /// </summary>
    public bool IsEstablished => OrganizationId.HasValue;

    /// <summary>
    /// The organization, or a failure if none was established.
    /// </summary>
    /// <remarks>
    /// Reaching this on an unestablished context means a feature ran outside
    /// the isolation context, which the database would answer with an empty
    /// result rather than an error. Failing loudly here turns that silence
    /// into something diagnosable.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No organization was established.</exception>
    public Guid RequireOrganizationId() =>
        OrganizationId ?? throw new InvalidOperationException(
            "No organization context was established for this request.");

    /// <summary>
    /// Set once, by the entry channel. Internal on purpose: a feature that
    /// could change the organization mid-request would be acting outside the
    /// context the database has already fixed for the transaction.
    /// </summary>
    internal void Establish(
        Guid organizationId,
        MembershipRole role,
        Guid userId,
        System.Net.IPAddress? ipAddress)
    {
        if (IsEstablished)
        {
            throw new InvalidOperationException(
                "The organization context is already established for this request.");
        }

        OrganizationId = organizationId;
        Role = role;
        UserId = userId;
        IpAddress = ipAddress;
    }
}
