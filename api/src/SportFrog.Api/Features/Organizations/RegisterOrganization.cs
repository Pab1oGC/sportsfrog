using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Features.Organizations;

/// <summary>
/// Registers an organization together with the user who owns it (RF-01).
///
/// This is how an organizer enters the platform, so it is the one operation
/// that necessarily runs with no session and no organization context: both
/// are what it creates.
/// </summary>
public static partial class RegisterOrganization
{
    /// <param name="Name">Display name of the organization.</param>
    /// <param name="Slug">First segment of its public address.</param>
    /// <param name="OwnerFullName">Name of the person who will own it.</param>
    /// <param name="OwnerEmail">Their address, unique across the platform.</param>
    /// <param name="OwnerPassword">Their password, kept only long enough to derive it.</param>
    public sealed record Request(
        string Name,
        string Slug,
        string OwnerFullName,
        string OwnerEmail,
        string OwnerPassword);

    public sealed record Response(Guid OrganizationId, Guid OwnerUserId);

    /// <summary>
    /// A slug appears in a public address, so it is restricted to what reads
    /// and travels well there: lowercase letters, digits and single hyphens
    /// between them.
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern { get; }

    private const int SlugMinimumLength = 3;
    private const int SlugMaximumLength = 63;

    public static IEndpointRouteBuilder MapRegisterOrganization(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/organizations", HandleAsync)
            // Nobody has an account yet, and the organization this would be
            // scoped to is the one being created.
            .AllowAnonymous()
            .WithoutOrganizationContext()
            .WithName(nameof(RegisterOrganization))
            .WithSummary("Registers an organization and its owner.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        BCryptPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var slug = request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

        if (Validate(request, slug) is { } invalid)
        {
            return invalid;
        }

        // A soft-deleted organization still holds its slug, and a soft-deleted
        // user still holds their address: both unique constraints span every
        // row, so the check has to look past the visibility filter or it would
        // promise a name the insert cannot take.
        if (await database.Organizations.IgnoreQueryFilters()
                .AnyAsync(existing => existing.Slug == slug, cancellationToken))
        {
            return Results.Problem(
                detail: "That address is already taken by another organization.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await database.Users.IgnoreQueryFilters()
                .AnyAsync(existing => existing.Email == request.OwnerEmail, cancellationToken))
        {
            return Results.Problem(
                detail: "An account already exists for that email address.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
        };

        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = request.OwnerEmail.Trim(),
            FullName = request.OwnerFullName.Trim(),
            PasswordHash = passwordHasher.Hash(request.OwnerPassword),
        };

        try
        {
            await PersistAsync(database, organization, owner, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Two registrations racing for the same slug or address. The
            // check above answers the ordinary case; the unique constraint
            // answers this one, and it is the database that has the last word.
            //
            // Narrowed to that one condition on purpose: catching every write
            // failure here would report a permission problem or an unreachable
            // database as a conflict, and hide a real fault behind a plausible
            // answer.
            return Results.Problem(
                detail: "That address or email address was taken while registering.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/organizations/{organization.Id}",
            new Response(organization.Id, owner.Id));
    }

    /// <summary>
    /// Writes the three rows as one unit.
    /// </summary>
    /// <remarks>
    /// The membership cannot be written before its organization exists, and
    /// it cannot be written outside that organization's isolation context:
    /// <c>organization_memberships</c> carries the policy, and the WITH CHECK
    /// half of it rejects a row whose org_id is not the established one. So
    /// the context is established here, mid-transaction, once there is an
    /// organization to establish it to.
    ///
    /// This is the third place a context is established outside the entry
    /// channel — with the public path and deferred jobs — and, like those, it
    /// is a consequence of the operation having no session to derive one from.
    /// </remarks>
    private static async Task PersistAsync(
        SportFrogDbContext database,
        Organization organization,
        User owner,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Neither table carries an isolation policy.
        database.Organizations.Add(organization);
        database.Users.Add(owner);
        await database.SaveChangesAsync(cancellationToken);

        await database.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_org', {0}, true)",
            [organization.Id.ToString()],
            cancellationToken);

        database.Memberships.Add(new OrganizationMembership
        {
            OrgId = organization.Id,
            UserId = owner.Id,

            // Whoever registers the organization owns it. Every other role is
            // granted from inside, by someone who already belongs.
            Role = MembershipRole.Owner,
        });
        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Rejects a malformed request before anything is written, reporting the
    /// first problem found.
    /// </summary>
    private static IResult? Validate(Request request, string slug)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Invalid("The organization name is required.");
        }

        if (slug.Length is < SlugMinimumLength or > SlugMaximumLength || !SlugPattern.IsMatch(slug))
        {
            return Invalid(
                $"The address must be between {SlugMinimumLength} and {SlugMaximumLength} " +
                "characters, using lowercase letters, digits and single hyphens.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerFullName))
        {
            return Invalid("The owner's name is required.");
        }

        if (!Email.TryParse(request.OwnerEmail?.Trim(), out _))
        {
            return Invalid("The email address is not well formed.");
        }

        return !Password.TryParse(request.OwnerPassword, out _)
            ? Invalid(
                $"The password must be at least {Password.MinimumLength} characters and " +
                "include an uppercase letter, a lowercase letter, a digit and a special character.")
            : null;
    }

    /// <summary>
    /// Whether the write failed because a unique constraint rejected it,
    /// rather than for any of the other reasons a write can fail.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static IResult Invalid(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
