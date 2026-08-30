using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Teams;

/// <summary>
/// Enters a club into a category of a competition.
/// </summary>
public static class CreateTeam
{
    /// <param name="Name">
    /// What the team plays under. Left out, the club's own name is used,
    /// which is what it is nearly always going to be — a club only needs its
    /// own name here when it fields two teams in the same draw.
    /// </param>
    public sealed record Request(Guid ClubId, string? Name, string? GroupLabel, short? Seed);

    public sealed record Response(Guid Id, string Name);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.ClubId)
                .NotEmpty().WithMessage("El club es obligatorio.");

            RuleFor(request => request.Name)
                .MaximumLength(120)
                .When(request => request.Name is not null);

            RuleFor(request => request.GroupLabel)
                .MaximumLength(40)
                .When(request => request.GroupLabel is not null);

            RuleFor(request => request.Seed)
                .InclusiveBetween((short)1, (short)26)
                .When(request => request.Seed.HasValue)
                .WithMessage("El bombo es un número entre 1 y 26.");
        }
    }

    public static IEndpointRouteBuilder MapCreateTeam(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/teams", HandleAsync)
            // Entering clubs is running the competition rather than
            // configuring it, so an operator can do it. Which categories exist
            // and what rules they play by stays with the administrator.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(CreateTeam))
            .WithSummary("Enters a club into a category.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Include(candidate => candidate.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { } competition)
        {
            return Results.NotFound();
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            // Once the competition is under way its table is being built from
            // matches already played, and a competitor entering now would
            // appear in it having played none of them. Before the first
            // whistle the draw can still absorb a late entry.
            return Results.Problem(
                detail: "Esta competencia ya está en curso, así que no se pueden inscribir más " +
                        "clubes. Un club que se sume ahora aparecería en una tabla que no jugó.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var club = await database.Clubs
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ClubId, cancellationToken);

        if (club is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ClubId"] = ["Ningún club de esta organización tiene ese identificador."],
            });
        }

        if (!club.IsActive)
        {
            // Deactivating a club is how an organization records that it no
            // longer competes. Letting it be entered anyway would make that
            // record meaningless.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ClubId"] = [$"{club.Name} no está activo, así que no se puede inscribir."],
            });
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? club.Name : request.Name.Trim();

        if (await database.Teams.AnyAsync(
                team => team.CategoryId == categoryId && team.ClubId == request.ClubId,
                cancellationToken))
        {
            return Results.Problem(
                detail: $"{club.Name} ya está inscripto en esta categoría. Un club que necesita " +
                        "dos equipos en un mismo sorteo se inscribe dos veces bajo clubes " +
                        "distintos, no dos veces bajo el mismo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            ClubId = club.Id,
            CategoryId = categoryId,
            Name = name,
            GroupLabel = string.IsNullOrWhiteSpace(request.GroupLabel)
                ? null
                : request.GroupLabel.Trim(),
            Seed = request.Seed,
        };

        database.Teams.Add(team);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two entries for the same club racing each other. The check above
            // settles the ordinary case; only the index sees this one.
            return Results.Problem(
                detail: $"{club.Name} ya está inscripto en esta categoría.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/teams/{team.Id}", new Response(team.Id, team.Name));
    }
}
