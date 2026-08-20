using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Features.Organizations;

/// <summary>
/// Adds a person to the active organization with a role (RF-02).
///
/// If the address already belongs to an account, that account gains a
/// membership rather than a second one being created. This is what lets a
/// referee or a delegate work in several leagues under one set of
/// credentials, holding a different role in each.
/// </summary>
public static class AddMember
{
    public sealed record Request(string Email, string FullName, string Password, string Role);

    public sealed record Response(Guid UserId, string Role, bool AccountCreated);

    public static IEndpointRouteBuilder MapAddMember(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/members", HandleAsync)
            // Deciding who belongs and with what authority is administration
            // of the organization, not operation of a competition.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(AddMember))
            .WithSummary("Adds a person to the active organization with a role.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        BCryptPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MembershipRole>(request.Role, ignoreCase: true, out var role))
        {
            return Invalid("The role is not one this organization defines.");
        }

        if (role == MembershipRole.Owner)
        {
            // Ownership is established by registering the organization and is
            // not something an administrator hands out. Allowing it here
            // would let an administrator create a peer they cannot then
            // remove.
            return Invalid("Ownership cannot be granted.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Invalid("The person's name is required.");
        }

        if (!Email.TryParse(request.Email?.Trim(), out var email))
        {
            return Invalid("The email address is not well formed.");
        }

        var organizationId = organization.RequireOrganizationId();

        // The account may already exist, from another organization. Past the
        // visibility filter, because a soft-deleted account still holds its
        // address and a new one could not take it.
        var existing = await database.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.Email == email.Value, cancellationToken);

        if (existing is { DeletedAt: not null })
        {
            return Results.Problem(
                detail: "That email address is not available.",
                statusCode: StatusCodes.Status409Conflict);
        }

        User user;
        bool accountCreated;

        if (existing is null)
        {
            if (!Password.TryParse(request.Password, out _))
            {
                return Invalid(
                    $"The password must be at least {Password.MinimumLength} characters and " +
                    "include an uppercase letter, a lowercase letter, a digit and a special character.");
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email.Value,
                FullName = request.FullName.Trim(),
                PasswordHash = passwordHasher.Hash(request.Password),
            };

            database.Users.Add(user);
            accountCreated = true;
        }
        else
        {
            if (await IsAlreadyMemberAsync(database, organizationId, existing.Id, cancellationToken))
            {
                // One membership per person per organization, which the
                // unique constraint also enforces. Changing someone's role is
                // a different operation from adding them.
                return Results.Problem(
                    detail: "That person already belongs to this organization.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            user = existing;
            accountCreated = false;
        }

        database.Memberships.Add(new OrganizationMembership
        {
            OrgId = organizationId,
            UserId = user.Id,
            Role = role,
        });

        await database.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/members/{user.Id}",
            new Response(user.Id, role.ToString().ToLowerInvariant(), accountCreated));
    }

    /// <summary>
    /// Whether the person already belongs here.
    /// </summary>
    /// <remarks>
    /// Reads only within the active organization, which is all the isolation
    /// context permits: whether they belong to some other organization is not
    /// this organization's business, and the policy would not return it
    /// anyway.
    /// </remarks>
    private static Task<bool> IsAlreadyMemberAsync(
        SportFrogDbContext database,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        database.Memberships.AnyAsync(
            membership => membership.OrgId == organizationId && membership.UserId == userId,
            cancellationToken);

    private static IResult Invalid(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
