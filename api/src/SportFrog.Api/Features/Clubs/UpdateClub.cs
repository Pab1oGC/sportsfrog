using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Clubs;

/// <summary>Edits a club of the active organization.</summary>
public static class UpdateClub
{
    public sealed record Request(string Name, string? ShortName, string? LogoUrl, bool IsActive);

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

    public static IEndpointRouteBuilder MapUpdateClub(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/clubs/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(UpdateClub))
            .WithSummary("Edits a club.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var club = await database.Clubs.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (club is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();

        if (await database.Clubs.AnyAsync(
                other => other.Id != id && other.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "A club with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        club.Name = name;
        club.ShortName = request.ShortName?.Trim();
        club.LogoUrl = request.LogoUrl?.Trim();

        // Deactivating is not deleting: an inactive club keeps its history and
        // stops being offered for new competitions.
        club.IsActive = request.IsActive;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
