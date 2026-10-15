using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// One way an endpoint accepts being reached by a scoped membership: the
/// route value named <see cref="RouteKey"/>, read as a
/// <see cref="ScopeTargetKind"/> and resolved to a competition, a club or a
/// course, checked against whichever of those the caller is scoped to by
/// <see cref="Axis"/>.
/// </summary>
/// <remarks>
/// An endpoint that accepts more than one way in — a team's own roster,
/// reachable by that competition's administrator and by that club's
/// delegate alike — lists one <see cref="ScopeRequirement"/> per way, and
/// <see cref="RequireScopeExtensions.RequireScope"/> grants access the
/// moment any one of them matches. Most endpoints list exactly one.
/// </remarks>
public readonly record struct ScopeRequirement(ScopeType Axis, ScopeTargetKind Kind, string RouteKey);

/// <summary>
/// Records the scope requirements an endpoint accepts, so they are visible
/// in the endpoint's metadata rather than only inside a closure — the same
/// reason <c>RequiredRole</c> exists next to <c>RequireRoleExtensions</c>.
/// </summary>
public sealed record RequiredScope(IReadOnlyList<ScopeRequirement> AnyOf);

public static class RequireScopeExtensions
{
    /// <summary>
    /// Narrows an already-required role to specific competitions, clubs or
    /// courses: refuses the request unless the caller's membership is
    /// organization-wide, or reaches at least one of <paramref name="anyOf"/>.
    /// </summary>
    /// <remarks>
    /// This is additional to <see cref="RequireRoleExtensions.RequireRole"/>,
    /// never a substitute for it: that still decides how much authority the
    /// role carries, and this only decides where it reaches. An endpoint
    /// with no scope requirement is unaffected and stays organization-wide
    /// for every membership, exactly as before this existed.
    ///
    /// An endpoint filter for the same reason <c>RequireRole</c> is one:
    /// policies run during <c>UseAuthorization</c>, before the organization
    /// context — and now the scopes within it — exist.
    /// </remarks>
    public static RouteHandlerBuilder RequireScope(
        this RouteHandlerBuilder builder,
        params ScopeRequirement[] anyOf)
    {
        if (anyOf.Length == 0)
        {
            throw new ArgumentException(
                "At least one scope requirement must be given.", nameof(anyOf));
        }

        var requirement = new RequiredScope(anyOf);
        builder.WithMetadata(requirement);

        builder.AddEndpointFilter(async (invocation, next) =>
        {
            var organization = invocation.HttpContext.RequestServices
                .GetRequiredService<OrganizationContext>();

            if (!organization.IsEstablished)
            {
                // Same conflict RequireRoleExtensions already guards against:
                // an endpoint cannot narrow a scope it declared it does not
                // have to begin with.
                throw new InvalidOperationException(
                    $"An endpoint cannot require a scope and opt out of the organization " +
                    $"context at the same time. Remove one of " +
                    $"{nameof(RequireScope)} or {nameof(WithoutOrganizationContextExtensions.WithoutOrganizationContext)}.");
            }

            if (organization.IsOrganizationWide)
            {
                // The ordinary case today, for every membership: no rows in
                // membership_scopes means no narrowing, so this check has
                // nothing to add.
                return await next(invocation);
            }

            var resolver = invocation.HttpContext.RequestServices
                .GetRequiredService<ScopeResolver>();

            foreach (var spec in requirement.AnyOf)
            {
                var id = ReadRouteGuid(invocation.HttpContext, spec.RouteKey);

                var resolved = await resolver.ResolveAsync(
                    spec.Kind, id, invocation.HttpContext.RequestAborted);

                if (resolved is not null && Reaches(spec.Axis, organization, resolved))
                {
                    return await next(invocation);
                }

                // Either nothing of that kind has this id, or it does but
                // the caller isn't scoped to it. Both fall through to the
                // same 403 below rather than a 404: whether the id exists
                // at all is not this check's to reveal.
            }

            return Results.Problem(
                // Nothing about which competition or club would have been
                // enough — the same reasoning RequireRole's own refusal
                // follows.
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        });

        return builder;
    }

    /// <summary>
    /// Whether <paramref name="organization"/>'s scopes reach
    /// <paramref name="target"/> along <paramref name="axis"/>.
    /// </summary>
    /// <remarks>
    /// Kept separate from the filter around it — no <see cref="HttpContext"/>,
    /// no database — so the one part of this feature with any real branching
    /// is exercised directly, the same way <c>MembershipRoleAuthority.Reaches</c>
    /// is tested without a request ever being built. Internal, and visible to
    /// the test assembly through this project's own
    /// <c>InternalsVisibleTo</c>, for exactly that reason.
    /// </remarks>
    internal static bool Reaches(ScopeType axis, OrganizationContext organization, ResolvedScopeTarget target) =>
        axis switch
        {
            ScopeType.Competition => target.CompetitionId is { } competitionId
                && organization.ScopedCompetitionIds.Contains(competitionId),
            ScopeType.Club => target.ClubId is { } clubId
                && organization.ScopedClubIds.Contains(clubId),
            ScopeType.Course => target.CourseId is { } courseId
                && organization.ScopedCourseIds.Contains(courseId),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unknown scope axis."),
        };

    /// <summary>
    /// The route value named <paramref name="routeKey"/>, as a
    /// <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// Throws rather than falling through to a refusal: every route this
    /// extension is used on constrains that segment with <c>:guid</c>, so
    /// routing itself refuses a request where it is absent or malformed
    /// before an endpoint filter ever runs. Reaching here without one means
    /// <see cref="RequireScope"/> was given a <see cref="ScopeRequirement.RouteKey"/>
    /// that does not match the endpoint's own route template — a wiring
    /// mistake, not a request the caller could have gotten right.
    /// </remarks>
    private static Guid ReadRouteGuid(HttpContext context, string routeKey)
    {
        if (context.Request.RouteValues.TryGetValue(routeKey, out var raw)
            && raw is not null
            && Guid.TryParse(raw.ToString(), out var id))
        {
            return id;
        }

        throw new InvalidOperationException(
            $"RequireScope names route value '{routeKey}', which this endpoint's route does " +
            "not provide as a GUID. Check the route template and the key passed to RequireScope.");
    }
}
