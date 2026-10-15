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
    /// <summary>
    /// Tracks whether <see cref="EstablishScopes"/> ran, separately from
    /// whether it found anything: a membership with no scope rows is a real,
    /// final answer — organization-wide — and must not look like "not read
    /// yet" just because its sets came back empty.
    /// </summary>
    private bool _scopesEstablished;

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
    /// The competitions the caller's role is narrowed to. Empty — the
    /// ordinary case — means it is not narrowed by competition at all.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="EstablishScopes"/>, separately from
    /// <see cref="Establish"/>: scopes are read from a table under row-level
    /// security and so can only be resolved once the transaction's isolation
    /// context is set, which happens after <see cref="Establish"/> runs (see
    /// <see cref="MembershipScopeReader"/>'s remarks). Empty until then.
    /// </remarks>
    public IReadOnlySet<Guid> ScopedCompetitionIds { get; private set; } = new HashSet<Guid>();

    /// <summary>
    /// The clubs the caller's role is narrowed to. Empty — the ordinary
    /// case — means it is not narrowed by club at all.
    /// </summary>
    public IReadOnlySet<Guid> ScopedClubIds { get; private set; } = new HashSet<Guid>();

    /// <summary>
    /// The courses the caller's role is narrowed to. Empty — the ordinary
    /// case — means it is not narrowed by course at all.
    /// </summary>
    public IReadOnlySet<Guid> ScopedCourseIds { get; private set; } = new HashSet<Guid>();

    /// <summary>
    /// Whether the caller's role reaches everywhere in the organization
    /// rather than being narrowed to specific competitions, clubs or
    /// courses. True for every membership today, and for any membership
    /// that has never had a scope granted.
    /// </summary>
    public bool IsOrganizationWide =>
        ScopedCompetitionIds.Count == 0 && ScopedClubIds.Count == 0 && ScopedCourseIds.Count == 0;

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
    /// <param name="userId">
    /// Null only for work a schedule started rather than a person — see
    /// <c>TuitionAccrualJob</c>. The audit log already tolerates that: its
    /// own <see cref="Infrastructure.Persistence.Entities.AuditEntry.UserId"/>
    /// is nullable for exactly this case.
    /// </param>
    internal void Establish(
        Guid organizationId,
        MembershipRole role,
        Guid? userId,
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

    /// <summary>
    /// Set once, by the entry channel, after the transaction's isolation
    /// context already lets <see cref="MembershipScopeReader"/> read the
    /// real answer. Internal for the same reason as <see cref="Establish"/>:
    /// a feature narrowing or widening its own reach mid-request would be
    /// deciding its own authority rather than having it decided for it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Called before <see cref="Establish"/>, or more than once for this
    /// request.
    /// </exception>
    internal void EstablishScopes(MembershipScopeSet scopes)
    {
        if (!IsEstablished)
        {
            throw new InvalidOperationException(
                "Scopes cannot be established before the organization context itself.");
        }

        if (_scopesEstablished)
        {
            throw new InvalidOperationException(
                "Scopes are already established for this request.");
        }

        ScopedCompetitionIds = scopes.CompetitionIds;
        ScopedClubIds = scopes.ClubIds;
        ScopedCourseIds = scopes.CourseIds;
        _scopesEstablished = true;
    }
}
