using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Features.Organizations;

/// <summary>
/// Registers an organization together with the user who owns it (RF-01).
///
/// Self-service sign-up is not offered today: every organization is brought
/// onto the platform by SportFrog itself, from the one account
/// <c>SeedPlatformOwner</c> seeds for exactly that (see
/// <see cref="PlatformAuthority"/>). The operation still runs with no
/// organization context — the one it creates does not exist yet, so there is
/// nothing to act "inside" — but it is no longer anonymous: refusing it here
/// with a plain 401/403 is what keeps this the one place a stray or leaked
/// account cannot mint an organization for itself.
/// </summary>
public static class RegisterOrganization
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
    /// Checked before the handler runs, by the group's validation filter.
    /// Living beside the contract it describes is the point of a vertical
    /// slice: what the operation accepts is readable in one place.
    /// </summary>
    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty()
                .WithMessage("El nombre de la organización es obligatorio.");

            RuleFor(request => request.OwnerFullName)
                .NotEmpty()
                .WithMessage("El nombre del responsable es obligatorio.");

            // Checked against the normalized form, since that is what gets
            // stored: an address typed in capitals is accepted and lowercased,
            // not rejected.
            RuleFor(request => request.Slug)
                .Must(slug => Slug.IsAcceptable(Slug.Normalize(slug)))
                .WithMessage(Slug.Requirement);

            RuleFor(request => request.OwnerEmail)
                .Must(email => Email.TryParse(email?.Trim(), out _))
                .WithMessage("El correo electrónico no tiene un formato válido.");

            RuleFor(request => request.OwnerPassword)
                .Must(password => Password.TryParse(password, out _))
                .WithMessage(
                    $"La contraseña debe tener al menos {Password.MinimumLength} caracteres e " +
                    "incluir una mayúscula, una minúscula, un número y un carácter especial.");
        }
    }

    public static IEndpointRouteBuilder MapRegisterOrganization(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/organizations", HandleAsync)
            // Authenticated, but deliberately not .RequireRole(): that
            // extension checks the role held in the organization the request
            // acts in, and this request does not act in one — the
            // organization it creates does not exist until it runs. Checked
            // by hand instead, against the one organization allowed to
            // create others (PlatformAuthority), inside the handler.
            .WithoutOrganizationContext()
            .RequireRateLimiting(RateLimitPolicies.Authentication)
            .WithName(nameof(RegisterOrganization))
            .WithSummary("Registers an organization and its owner.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        HttpContext httpContext,
        SportFrogDbContext database,
        BCryptPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        // Same 401 the entry channel already gives every other endpoint that
        // requires a session — this one only differs in never establishing
        // an organization context, so it has to ask for a session itself.
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(
                detail: "Se requiere autenticación.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var platformRole = await PlatformAuthority.RoleForAsync(
            httpContext.User, database, cancellationToken);

        if (platformRole is not { } role || !role.Reaches(MembershipRole.Admin))
        {
            // Same wording .RequireRole() uses for the equivalent refusal —
            // nothing about which role would have been enough.
            return Results.Problem(
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var slug = Slug.Normalize(request.Slug);

        // A soft-deleted organization still holds its slug, and a soft-deleted
        // user still holds their address: both unique constraints span every
        // row, so the check has to look past the visibility filter or it would
        // promise a name the insert cannot take.
        if (await database.Organizations.IgnoreQueryFilters()
                .AnyAsync(existing => existing.Slug == slug, cancellationToken))
        {
            return Results.Problem(
                detail: "Esa dirección ya está tomada por otra organización.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (await database.Users.IgnoreQueryFilters()
                .AnyAsync(existing => existing.Email == request.OwnerEmail, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe una cuenta con ese correo electrónico.",
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
                detail: "Esa dirección o correo electrónico se tomó mientras se registraba.",
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
    /// Whether the write failed because a unique constraint rejected it,
    /// rather than for any of the other reasons a write can fail.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
