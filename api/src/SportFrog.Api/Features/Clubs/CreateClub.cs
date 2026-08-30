using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Clubs;

/// <summary>Registers a club in the active organization (RF-07).</summary>
public static class CreateClub
{
    /// <param name="LogoUrl">
    /// A data URL, the way a form sends an upload — not a link. Stored as a
    /// key into object storage, same as an athlete's photograph, and for the
    /// same reason: a column of arbitrary URLs is one dead link away from an
    /// empty crest on every table and bracket that names this club.
    /// </param>
    public sealed record Request(string Name, string? ShortName, string? LogoUrl);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre del club es obligatorio.")
                .MaximumLength(120);

            RuleFor(request => request.ShortName)
                .MaximumLength(20)
                .When(request => request.ShortName is not null)
                .WithMessage("La abreviatura tiene como máximo 20 caracteres.");

            RuleFor(request => request.LogoUrl)
                .Must(InlinePhoto.IsAcceptable)
                .When(request => request.LogoUrl is not null)
                .WithMessage(InlinePhoto.Requirement);
        }
    }

    public static IEndpointRouteBuilder MapCreateClub(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/clubs", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(CreateClub))
            .WithSummary("Registers a club.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        ClubPhoto photos,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        // Without IgnoreQueryFilters, unlike the checks on accounts: the
        // unique index here is partial, so a name freed by a logical deletion
        // is available again and the visibility filter says exactly that.
        if (await database.Clubs.AnyAsync(club => club.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe un club con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Settled here rather than by the database: the crest is filed under
        // it, and the picture has to be stored before the row that points at
        // it exists.
        var clubId = Guid.NewGuid();

        string? logoKey = null;

        if (request.LogoUrl is { Length: > 0 } upload)
        {
            logoKey = await photos.StoreAsync(clubId, upload, cancellationToken);

            if (logoKey is null)
            {
                return ClubPhoto.NotAnImage();
            }
        }

        var club = new Club
        {
            Id = clubId,
            OrgId = organization.RequireOrganizationId(),
            Name = name,
            ShortName = request.ShortName?.Trim(),
            LogoUrl = logoKey,
        };

        database.Clubs.Add(club);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created($"/clubs/{club.Id}", new Response(club.Id));
    }
}
