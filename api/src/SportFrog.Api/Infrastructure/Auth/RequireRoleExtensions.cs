using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// Records the least authority an endpoint accepts, so it is visible in the
/// endpoint's metadata rather than only inside a closure.
/// </summary>
public sealed record RequiredRole(MembershipRole Minimum);

public static class RequireRoleExtensions
{
    /// <summary>
    /// Refuses the request unless the caller's role in the active
    /// organization reaches <paramref name="minimum"/>.
    /// </summary>
    /// <remarks>
    /// An endpoint filter rather than an authorization policy, and that is
    /// forced by ordering: policies run during UseAuthorization, before the
    /// organization context exists, so at that point there is no role to
    /// check. The role is a fact about a person *within an organization*, and
    /// which organization only becomes known once the entry channel has
    /// resolved it.
    /// </remarks>
    public static RouteHandlerBuilder RequireRole(
        this RouteHandlerBuilder builder,
        MembershipRole minimum)
    {
        builder.WithMetadata(new RequiredRole(minimum));

        builder.AddEndpointFilter(async (invocation, next) =>
        {
            var organization = invocation.HttpContext.RequestServices
                .GetRequiredService<OrganizationContext>();

            if (organization.Role is not { } held)
            {
                // The entry channel refuses anything reaching here without a
                // context, so arriving without one means this endpoint was
                // also marked as not needing one. That combination asks for a
                // role inside an organization it declared it does not have.
                throw new InvalidOperationException(
                    $"An endpoint cannot require a role and opt out of the organization " +
                    $"context at the same time. Remove one of " +
                    $"{nameof(RequireRole)} or {nameof(WithoutOrganizationContextExtensions.WithoutOrganizationContext)}.");
            }

            return held.Reaches(minimum)
                ? await next(invocation)
                : Results.Problem(
                    // Nothing about which role would have been enough: that
                    // would describe the shape of the permission system to
                    // someone probing it.
                    detail: "Esta operación no está permitida.",
                    statusCode: StatusCodes.Status403Forbidden);
        });

        return builder;
    }
}
