namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Role a user holds *within one organization*. Authorization is never
/// global: the same person can be an operator in one league and a viewer in
/// another, which is the real case of referees and delegates (RF-02).
///
/// Mapped to the PostgreSQL <c>membership_role</c> enum. The member names
/// translate to their snake_case counterparts in the database.
/// </summary>
public enum MembershipRole
{
    /// <summary>Created the organization. Cannot be removed or demoted.</summary>
    Owner,

    /// <summary>Full administration of the organization.</summary>
    Admin,

    /// <summary>Runs competitions: fixtures, teams, rosters.</summary>
    Operator,

    /// <summary>Records match results only.</summary>
    Recorder,

    /// <summary>Read-only access to the administrative panel.</summary>
    Viewer,
}
