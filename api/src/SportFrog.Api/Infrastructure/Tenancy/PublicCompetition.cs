namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// A competition the public view is allowed to show, and the organization it
/// belongs to.
///
/// Obtaining one is the only way the public path learns an organization
/// identifier: the address carries readable slugs, never identifiers, and the
/// resolution already checked that the organization is active and the
/// competition is publishable.
/// </summary>
public sealed record PublicCompetition(Guid OrganizationId, Guid CompetitionId);
