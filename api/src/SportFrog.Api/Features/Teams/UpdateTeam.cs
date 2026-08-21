using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Teams;

/// <summary>
/// Corrects a team.
/// </summary>
/// <remarks>
/// The club and the category are not here. A team is the meeting of those
/// two; changing either does not correct this team, it describes a different
/// one. Entering the same club in another category is another entry, and it
/// leaves this one's fixtures where they are.
/// </remarks>
public static class UpdateTeam
{
    /// <param name="IsActive">
    /// False for a team that withdraws mid-season. It stops being scheduled
    /// and keeps everything it already played, which is why this is a flag
    /// and not a deletion.
    /// </param>
    public sealed record Request(string Name, string? GroupLabel, bool IsActive);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The team name is required.")
                .MaximumLength(120);

            RuleFor(request => request.GroupLabel)
                .MaximumLength(40)
                .When(request => request.GroupLabel is not null);
        }
    }

    public static IEndpointRouteBuilder MapUpdateTeam(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/teams/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(UpdateTeam))
            .WithSummary("Corrects a team.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var team = await database.Teams.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        team.Name = request.Name.Trim();
        team.GroupLabel = string.IsNullOrWhiteSpace(request.GroupLabel)
            ? null
            : request.GroupLabel.Trim();
        team.IsActive = request.IsActive;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
