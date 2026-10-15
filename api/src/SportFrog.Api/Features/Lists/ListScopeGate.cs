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
/// competition that category belongs to, any declaring a
/// <see cref="ListParameterKind.Team"/> one is gated on either axis that
/// team belongs to — the same dual reach <c>ReadTeams</c> and
/// <c>ReadTeamReport</c> already grant a team — and any declaring a
/// <see cref="ListParameterKind.Course"/> one is gated the same single way
/// <c>RequireScope</c> already gates a route's own <c>courseId</c>. A
/// provider adding a new parameter of any of the three kinds is gated this
/// same way automatically, with nothing here to change.
///
/// A provider declaring none of the three — <see cref="Providers.AthletesList"/>
/// and <see cref="Providers.TuitionArrearsList"/> today — has no single
/// target to gate on: <c>ReadLists</c> narrows its rows for a scoped
/// membership instead of refusing it outright, which is a different
/// question from the one this class answers.
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

        if (provider.Parameters.FirstOrDefault(parameter => parameter.Kind == ListParameterKind.Course) is { } course)
        {
            return scope.GetGuid(course.Name) is { } courseId
                && await ReachesAsync(
                    ScopeType.Course, ScopeTargetKind.Course, courseId, organization, resolver, cancellationToken);
        }

        // None of the three kinds declared: there is no single target here
        // to gate — see the class remarks.
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
