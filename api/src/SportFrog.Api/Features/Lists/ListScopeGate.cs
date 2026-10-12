using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Lists;

/// <summary>
/// Whether a scoped membership may run one list at all — the question
/// <see cref="RequireScopeExtensions.RequireScope"/> answers for a route
/// value, asked the same way here for whichever parameter a provider
/// declares.
/// </summary>
/// <remarks>
/// Driven entirely by <see cref="ListParameterKind"/>, never by which
/// provider is asking: any provider declaring a
/// <see cref="ListParameterKind.Category"/> parameter is gated on the
/// competition that category belongs to, and any declaring a
/// <see cref="ListParameterKind.Team"/> one is gated on either axis that
/// team belongs to — the same dual reach <c>ReadTeams</c> and
/// <c>ReadTeamReport</c> already grant a team. A provider adding a new
/// parameter of either kind is gated this same way automatically, with
/// nothing here to change.
///
/// A provider declaring neither — today, only
/// <see cref="Providers.AthletesList"/> — has no single target to gate on:
/// <c>ReadLists</c> narrows its rows for a scoped membership instead of
/// refusing it outright, which is a different question from the one this
/// class answers.
/// </remarks>
internal static class ListScopeGate
{
    public static async Task<bool> ReachesAsync(
        IListProvider provider,
        ListScope scope,
        OrganizationContext organization,
        ScopeResolver resolver,
        CancellationToken cancellationToken)
    {
        if (organization.IsOrganizationWide)
        {
            return true;
        }

        if (provider.Parameters.FirstOrDefault(parameter => parameter.Kind == ListParameterKind.Category) is { } category)
        {
            return scope.GetGuid(category.Name) is { } categoryId
                && await ReachesAsync(
                    ScopeType.Competition, ScopeTargetKind.Category, categoryId, organization, resolver, cancellationToken);
        }

        if (provider.Parameters.FirstOrDefault(parameter => parameter.Kind == ListParameterKind.Team) is { } team)
        {
            if (scope.GetGuid(team.Name) is not { } teamId)
            {
                return false;
            }

            var resolved = await resolver.ResolveAsync(ScopeTargetKind.Team, teamId, cancellationToken);

            return resolved is not null
                && (RequireScopeExtensions.Reaches(ScopeType.Competition, organization, resolved)
                    || RequireScopeExtensions.Reaches(ScopeType.Club, organization, resolved));
        }

        // Neither kind declared: there is no single target here to gate —
        // see the class remarks.
        return true;
    }

    private static async Task<bool> ReachesAsync(
        ScopeType axis,
        ScopeTargetKind kind,
        Guid id,
        OrganizationContext organization,
        ScopeResolver resolver,
        CancellationToken cancellationToken)
    {
        var resolved = await resolver.ResolveAsync(kind, id, cancellationToken);
        return resolved is not null && RequireScopeExtensions.Reaches(axis, organization, resolved);
    }
}
