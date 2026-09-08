using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Features.Clubs;
using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Teams;

/// <summary>
/// Enters one athlete, a pair, or a small team into a category of an
/// individual sport, in one step.
/// </summary>
/// <remarks>
/// What <see cref="CreateTeam"/> and <see cref="Rosters.RegisterPlayer"/> do
/// in two calls for a squad — enter the club, then register a player onto
/// it — collapses into one here, because for an individual sport the team is
/// never a separate decision: it is a team of however many athletes are
/// being entered together, created to hold exactly them, and nobody would
/// ever create one on its own.
///
/// One or several athletes at once rather than always one: poomsae runs
/// individual, pairs and team performances under the same
/// <c>sports.is_individual</c> flag, and a pair entered as two separate
/// requests could succeed on the first and fail on the second, leaving a
/// "pair" with one member and no clean way back. Entering them together
/// makes it one fact recorded once, or not at all.
///
/// A member can still be added afterwards through the ordinary
/// <see cref="Rosters.RegisterPlayer"/> — a delegation confirming a second
/// partner after a deadline is a real case — which is why
/// <see cref="Category.MaxRosterSize"/>, not this endpoint, is what actually
/// caps how many a category's teams may hold.
/// </remarks>
public static class EnrollIndividual
{
    /// <param name="AthleteIds">
    /// Who performs together as this team: one for an individual entry, two
    /// for a pair, more for a larger one. Order has no meaning.
    /// </param>
    /// <param name="ClubId">
    /// The delegation the team competes for. Left out, the organization's
    /// unaffiliated club is used — a completely ordinary way to enter, for a
    /// sport where a team need not belong to any delegation at all.
    /// </param>
    public sealed record Request(
        IReadOnlyList<Guid> AthleteIds, Guid? ClubId, string? GroupLabel, short? Seed);

    public sealed record Response(Guid TeamId, IReadOnlyList<Guid> RosterEntryIds, string Name);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.AthleteIds)
                .NotEmpty().WithMessage("Hay que indicar al menos un deportista.");

            RuleFor(request => request.AthleteIds)
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .When(request => request.AthleteIds.Count > 0)
                .WithMessage("El mismo deportista está repetido en la lista.");

            RuleFor(request => request.GroupLabel)
                .MaximumLength(40)
                .When(request => request.GroupLabel is not null);

            RuleFor(request => request.Seed)
                .InclusiveBetween((short)1, (short)26)
                .When(request => request.Seed.HasValue)
                .WithMessage("El bombo es un número entre 1 y 26.");
        }
    }

    public static IEndpointRouteBuilder MapEnrollIndividual(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/categories/{categoryId:guid}/individuals", HandleAsync)
            // Same actor as entering a club: this is running the
            // competition, not configuring it.
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(EnrollIndividual))
            .WithSummary("Enters an individual, pair or team into a category of an individual sport.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        Request request,
        SportFrogDbContext database,
        RosterPolicy policy,
        UnaffiliatedClub unaffiliatedClub,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories
            .AsNoTracking()
            .Include(candidate => candidate.Competition)
                .ThenInclude(competition => competition!.Sport)
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category?.Competition is not { Sport: { } sport } competition)
        {
            return Results.NotFound();
        }

        if (!sport.IsIndividual)
        {
            return Results.Problem(
                detail: $"{sport.Name} no es un deporte individual, así que sus categorías se " +
                        "inscriben por club — con /categories/{id}/teams — y no por deportista.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Scheduled))
        {
            // Same reasoning as CreateTeam: before the draw, a late entrant
            // can still be absorbed into it. Once the competition is under
            // way its table is being built from bouts already fought, and an
            // entrant who joins now would appear in it having fought none.
            return Results.Problem(
                detail: "Esta competencia ya está en curso, así que no se pueden inscribir más " +
                        "deportistas. Uno que se sume ahora aparecería en un cuadro que no jugó.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var athletesById = await database.Athletes
            .AsNoTracking()
            .Where(candidate => request.AthleteIds.Contains(candidate.Id))
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);

        var missing = request.AthleteIds.Where(id => !athletesById.ContainsKey(id)).ToList();

        if (missing.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteIds"] = [.. missing.Select(id =>
                    $"Ningún deportista de esta organización tiene el identificador {id}.")],
            });
        }

        // Preserves the order the request named them in, now that every id
        // is known to resolve to a real athlete.
        var athletes = request.AthleteIds.Select(id => athletesById[id]).ToList();

        Club club;

        if (request.ClubId is { } clubId)
        {
            var found = await database.Clubs
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == clubId, cancellationToken);

            if (found is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ClubId"] = ["Ningún club de esta organización tiene ese identificador."],
                });
            }

            if (!found.IsActive)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ClubId"] = [$"{found.Name} no está activo, así que no se puede inscribir bajo él."],
                });
            }

            club = found;
        }
        else
        {
            club = await unaffiliatedClub.EnsureAsync(organization.RequireOrganizationId(), cancellationToken);
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            ClubId = club.Id,
            CategoryId = categoryId,
            Name = IndividualTeamName.From(athletes.Select(athlete => (athlete.FirstName, athlete.LastName))),
            GroupLabel = string.IsNullOrWhiteSpace(request.GroupLabel)
                ? null
                : request.GroupLabel.Trim(),
            Seed = request.Seed,
            IsIndividual = true,
        };

        // Not persisted yet: RosterPolicy's checks either look across the
        // whole category (sex, birth date, the same-athlete collision) or
        // count what is already registered against this specific team, which
        // for a team created in this same request is always zero. `pending`
        // is the position already reached in this same list — the same
        // parameter a bulk import passes so the second member of a pair sees
        // the first one already counted against the cap, rather than each
        // being checked as if entering alone.
        var violationsByAthlete = new Dictionary<string, string[]>();

        for (var index = 0; index < athletes.Count; index++)
        {
            var violations = await policy.InspectAsync(
                team, category, athletes[index], jerseyNumber: null, excluding: null, cancellationToken,
                pending: index);

            if (violations.Count > 0)
            {
                violationsByAthlete[$"AthleteIds[{index}]"] =
                    [.. violations.Select(violation => violation.Message)];
            }
        }

        if (violationsByAthlete.Count > 0)
        {
            return Results.ValidationProblem(violationsByAthlete);
        }

        var entries = athletes.Select(athlete => new RosterEntry
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            TeamId = team.Id,
            AthleteId = athlete.Id,
        }).ToList();

        database.Teams.Add(team);
        database.RosterEntries.AddRange(entries);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // One of these athletes was entered elsewhere in the same
            // instant, racing this request. The checks above settle the
            // ordinary case; only the index sees this one. Nothing in this
            // team was saved — one failed insert rolls the whole team back,
            // which is the same "all or nothing" the checks above already
            // promised.
            return Results.Problem(
                detail: "Una de estas inscripciones choca con una hecha en el mismo instante. " +
                        "Volvé a leer la categoría e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/teams/{team.Id}",
            new Response(team.Id, [.. entries.Select(entry => entry.Id)], team.Name));
    }
}
