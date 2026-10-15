using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// The competitions, clubs and courses one membership's role is narrowed to.
/// </summary>
/// <remarks>
/// All three sets empty — <see cref="OrganizationWide"/> — is the ordinary
/// case and means the membership is not narrowed at all: it reaches its role
/// everywhere in the organization, the same as before this type existed. A
/// membership scoped to several targets of the same kind simply has several
/// entries in that one set; there is no row-per-target plumbing visible here.
///
/// <paramref name="CourseIds"/> defaults to empty rather than being
/// required, so every call site built before the course scope existed —
/// this record's own two-argument constructions in <see cref="ReadAsync"/>
/// and across the test suite — keeps compiling unchanged.
/// </remarks>
public sealed record MembershipScopeSet(
    IReadOnlySet<Guid> CompetitionIds, IReadOnlySet<Guid> ClubIds, IReadOnlySet<Guid>? CourseIds = null)
{
    public IReadOnlySet<Guid> CourseIds { get; init; } = CourseIds ?? new HashSet<Guid>();

    public static readonly MembershipScopeSet OrganizationWide =
        new(new HashSet<Guid>(), new HashSet<Guid>());

    /// <summary>
    /// Whether this membership carries no narrowing at all. Checking this
    /// rather than each set individually is what keeps "no rows" reading as
    /// one deliberate case instead of three coincidentally-empty ones.
    /// </summary>
    public bool IsOrganizationWide => CompetitionIds.Count == 0 && ClubIds.Count == 0 && CourseIds.Count == 0;
}

/// <summary>
/// Reads the scopes a membership holds, for
/// <see cref="OrganizationContext"/> to establish once per request.
/// </summary>
/// <remarks>
/// Deliberately a database read on every request rather than something
/// carried in the access token: a role can live in the token because
/// granting or revoking it only has to take effect at the next sign-in, but
/// a scope has to take effect immediately — the whole reason a scoped
/// membership is safe to hand to a club's own delegate is that narrowing or
/// revoking it cannot wait for a token to expire.
/// </remarks>
public sealed class MembershipScopeReader(SportFrogDbContext database)
{
    /// <summary>
    /// Reads the caller's scopes for the organization the current
    /// transaction is isolated to.
    /// </summary>
    /// <remarks>
    /// Must run after the isolation context has been set for the current
    /// transaction (DD-02): <c>membership_scopes</c> and
    /// <c>organization_memberships</c> both carry row-level security, so
    /// calling this before <c>set_config('app.current_org', ...)</c> would
    /// see no rows and read as "organization-wide" for a membership that is
    /// actually narrowed — opening access instead of narrowing it. There is
    /// no org_id parameter here for that reason: this reads whichever
    /// organization the already-open transaction is isolated to, never a
    /// caller-supplied one.
    /// </remarks>
    public async Task<MembershipScopeSet> ReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await database.MembershipScopes
            .Where(scope => scope.Membership!.UserId == userId)
            .Select(scope => new { scope.ScopeType, scope.CompetitionId, scope.ClubId, scope.CourseId })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return MembershipScopeSet.OrganizationWide;
        }

        var competitionIds = rows
            .Where(row => row.ScopeType == ScopeType.Competition)
            .Select(row => row.CompetitionId!.Value)
            .ToHashSet();

        var clubIds = rows
            .Where(row => row.ScopeType == ScopeType.Club)
            .Select(row => row.ClubId!.Value)
            .ToHashSet();

        var courseIds = rows
            .Where(row => row.ScopeType == ScopeType.Course)
            .Select(row => row.CourseId!.Value)
            .ToHashSet();

        return new MembershipScopeSet(competitionIds, clubIds, courseIds);
    }
}
