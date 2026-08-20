using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Clubs;

/// <summary>Registers a club in the active organization (RF-07).</summary>
public static class CreateClub
{
    public sealed record Request(string Name, string? ShortName, string? LogoUrl);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The club name is required.")
                .MaximumLength(120);

            RuleFor(request => request.ShortName)
                .MaximumLength(20)
                .When(request => request.ShortName is not null)
                .WithMessage("The short name must be at most 20 characters.");
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
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        // Without IgnoreQueryFilters, unlike the checks on accounts: the
        // unique index here is partial, so a name freed by a logical deletion
        // is available again and the visibility filter says exactly that.
        if (await database.Clubs.AnyAsync(club => club.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "A club with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var club = new Club
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            Name = name,
            ShortName = request.ShortName?.Trim(),
            LogoUrl = request.LogoUrl?.Trim(),
        };

        database.Clubs.Add(club);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created($"/clubs/{club.Id}", new Response(club.Id));
    }
}
