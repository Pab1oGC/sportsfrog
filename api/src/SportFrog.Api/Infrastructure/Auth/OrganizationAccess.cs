using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// One organization a user belongs to, and the single role they hold in it.
///
/// Authorization is resolved per organization and never globally: the same
/// person can be an operator in one league and a viewer in another, which is
/// the real case of referees and delegates the design calls out. A token that
/// listed roles without saying where each one applies could not express that.
///
/// One role per organization, matching the unique constraint on
/// (org_id, user_id) in the database.
/// </summary>
public sealed record OrganizationAccess(Guid OrganizationId, MembershipRole Role);
