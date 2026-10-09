namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// Scope checks for a request whose targets are named in its body rather
/// than its route — <see cref="RequireScopeExtensions.RequireScope"/>'s own
/// mechanism only ever answers for one route value, so a batch naming
/// several competitions at once (a bulk reschedule) needs this instead.
/// </summary>
public static class OrganizationContextExtensions
{
    /// <summary>
    /// Whether <paramref name="organization"/> reaches every one of
    /// <paramref name="competitionIds"/>.
    /// </summary>
    /// <remarks>
    /// "All", not "any": a batch that touches one competition the caller is
    /// scoped to and one it is not must refuse as a whole, the same
    /// principle its own conflict check already follows — nothing in it is
    /// ever compared against, or applied on top of, a half-applied state.
    /// An organization-wide membership reaches every competition trivially,
    /// matching <see cref="OrganizationContext.IsOrganizationWide"/>
    /// everywhere else.
    /// </remarks>
    public static bool ReachesAllCompetitions(
        this OrganizationContext organization, IEnumerable<Guid> competitionIds) =>
        organization.IsOrganizationWide || competitionIds.All(organization.ScopedCompetitionIds.Contains);
}
