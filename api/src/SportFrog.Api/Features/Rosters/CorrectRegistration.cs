using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Corrects the shirt or the position of a registration.
/// </summary>
/// <remarks>
/// The person is not editable here, and that is the whole shape of it: a
/// registration is the fact that <em>this</em> athlete plays for
/// <em>this</em> team. Pointing it at somebody else would not correct it, it
/// would rewrite who scored the goals already recorded against it. Registering
/// the other person is a separate registration.
///
/// Only the shirt is re-checked. Age, sex and the squad cap were settled when
/// the player was registered, and re-litigating them here would block a
/// correction that has nothing to do with them — the number cannot be fixed
/// on a player whose club deactivated them last week, for a reason that has
/// nothing to do with the number.
/// </remarks>
public static class CorrectRegistration
{
    public sealed record Request(short? JerseyNumber, string? Position);

    internal sealed class Validator : AbstractValidator<Request>
    {
        private const short MaximumJerseyNumber = 999;

        public Validator()
        {
            RuleFor(request => request.JerseyNumber)
                .InclusiveBetween((short)0, MaximumJerseyNumber)
                .When(request => request.JerseyNumber.HasValue)
                .WithMessage($"El número de camiseta está entre 0 y {MaximumJerseyNumber}, o se " +
                             "deja sin definir hasta que se repartan los números.");

            RuleFor(request => request.Position)
                .MaximumLength(40)
                .When(request => request.Position is not null);
        }
    }

    public static IEndpointRouteBuilder MapCorrectRegistration(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/roster/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(CorrectRegistration))
            .WithSummary("Corrects the shirt or position of a registration.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        RosterPolicy policy,
        CancellationToken cancellationToken)
    {
        var entry = await database.RosterEntries
            .Include(candidate => candidate.Team)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (entry?.Team is not { } team)
        {
            return Results.NotFound();
        }

        if (await policy.InspectJerseyAsync(
                team, request.JerseyNumber, excluding: id, cancellationToken) is { } violation)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [violation.Property] = [violation.Message],
            });
        }

        entry.JerseyNumber = request.JerseyNumber;
        entry.Position = string.IsNullOrWhiteSpace(request.Position)
            ? null
            : request.Position.Trim();

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two corrections claiming one number. The check above answers the
            // ordinary case; only the partial index sees this one.
            return Results.Problem(
                detail: "Esa camiseta se tomó en el mismo instante. Volvé a leer la nómina e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
