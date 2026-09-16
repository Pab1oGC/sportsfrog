using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Features.Teams;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Registers several people to play for a team in one step.
/// </summary>
/// <remarks>
/// <see cref="RegisterPlayer"/> already does this one person at a time, and
/// still is how a jersey number or a position gets set — this exists for the
/// moment before any of that matters: a delegate handing over several names
/// at once, the way the spreadsheet import already accepts a squad in one
/// file (see <see cref="Import.ApplyRosterImport"/>), for people who are
/// already on the register and do not need one built from a row of a sheet.
///
/// All or nothing, the same promise the spreadsheet import makes: five people
/// checked one at a time against a roster cap of three would let the first
/// three in and refuse the last two for a reason that has nothing to do with
/// them — the team simply is not getting this squad. Refusing all five and
/// naming exactly why is what actually helps whoever is entering them.
///
/// No jersey or position here, unlike the single registration — there is no
/// one number to ask for when several people are being read from the same
/// list at once. Those are set afterwards, per person, the same way a
/// spreadsheet-imported row is corrected: through <see cref="CorrectRegistration"/>.
/// </remarks>
public static class RegisterPlayersBulk
{
    public sealed record Request(IReadOnlyList<Guid> AthleteIds);

    public sealed record Response(IReadOnlyList<Guid> RosterEntryIds);

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
        }
    }

    public static IEndpointRouteBuilder MapRegisterPlayersBulk(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/roster/register-bulk", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RegisterPlayersBulk))
            .WithSummary("Registers several people to play for a team in one step.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        Request request,
        SportFrogDbContext database,
        RosterPolicy policy,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await RosterTeamGate.OpenAsync(teamId, database, cancellationToken) is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var (team, category) = (opened.Team!, opened.Category!);

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

        // Preserves the order the request named them in.
        var athletes = request.AthleteIds.Select(id => athletesById[id]).ToList();

        // Not persisted yet: RosterPolicy's checks either look across the
        // whole category (sex, birth date, the same-athlete collision) or
        // count what is already registered against this specific team, which
        // for someone about to be added in this same request would otherwise
        // read as zero for every one of them. `pending` is the position
        // already reached in this same list — same parameter EnrollIndividual
        // passes for the identical reason, so the second name in the batch
        // sees the first one already counted against the cap, rather than
        // each being checked as if arriving alone.
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

        database.RosterEntries.AddRange(entries);

        if (team.IsIndividual)
        {
            // Same reasoning as RegisterPlayer: no other source for this
            // team's name exists, so picking up members — one or several at
            // once — is exactly the moment it has to be recomputed.
            var teammates = await database.RosterEntries
                .AsNoTracking()
                .Where(candidate => candidate.TeamId == team.Id && candidate.WithdrawnAt == null)
                .Select(candidate => new { candidate.Athlete!.FirstName, candidate.Athlete.LastName })
                .ToListAsync(cancellationToken);

            team.Name = IndividualTeamName.From(
                teammates
                    .Select(member => (member.FirstName, member.LastName))
                    .Concat(athletes.Select(athlete => (athlete.FirstName, athlete.LastName))));
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // One of these people was registered elsewhere in the same
            // instant, racing this request. The checks above settle the
            // ordinary case; only the index sees this one. Nothing in this
            // batch was saved — one failed insert rolls the whole batch back,
            // the same "all or nothing" the checks above already promised.
            return Results.Problem(
                detail: "Uno de estos registros choca con uno hecho en el mismo instante. Volvé a " +
                        "leer la nómina e intentá de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/teams/{team.Id}/roster",
            new Response([.. entries.Select(entry => entry.Id)]));
    }
}
