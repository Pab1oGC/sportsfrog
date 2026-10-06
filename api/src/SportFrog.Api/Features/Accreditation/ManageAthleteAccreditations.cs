using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Accreditation;

/// <summary>
/// Which accreditation category each athlete of a competition holds.
/// </summary>
/// <remarks>
/// Scoped to the competition's active roster — the same population
/// <see cref="Documents.IssueDocumentsJob"/> reads to print from — because
/// accrediting somebody who is not entered in this competition at all is not
/// a smaller version of the real operation, it is a different one: there is
/// no team, no category of play, nothing the credential could print under
/// "team" or "club". Registering them for the roster is the way in.
///
/// One assignment at a time through <see cref="UpsertAsync"/>, or the whole
/// roster at once through <see cref="AssignCategoryBulk"/> — the shape real
/// accreditation work actually takes: a category given to everyone first, a
/// handful corrected afterwards one by one.
/// </remarks>
public static class ManageAthleteAccreditations
{
    public sealed record AssignRequest(Guid CategoryId);

    public sealed record AssignResponse(Guid AccreditationId);

    /// <summary>One row of the competition's roster, with its accreditation if it has one.</summary>
    /// <remarks>
    /// Built from the roster outward rather than from existing accreditations
    /// inward, so a person nobody has accredited yet still appears — with
    /// <see cref="CategoryId"/> absent — instead of being invisible to a
    /// screen whose whole purpose is to show who still needs one.
    /// </remarks>
    public sealed record RosterSummary(
        Guid AthleteId,
        string FirstName,
        string LastName,
        string TeamName,
        Guid? AccreditationId,
        Guid? CategoryId,
        string? CategoryCode,
        string? CategoryName,
        int OverrideCount);

    public sealed record DetailResponse(
        Guid AccreditationId,
        Guid CategoryId,
        string CategoryCode,
        string CategoryName,
        IReadOnlyList<ResolvedAccreditationItem> Resolved);

    internal sealed class AssignValidator : AbstractValidator<AssignRequest>
    {
        public AssignValidator() =>
            RuleFor(request => request.CategoryId)
                .NotEmpty().WithMessage("La categoría de acreditación es obligatoria.");
    }

    public sealed record BulkAssignRequest(IReadOnlyList<Guid> AthleteIds);

    public sealed record BulkAssignResponse(IReadOnlyList<Guid> AccreditationIds);

    internal sealed class BulkAssignValidator : AbstractValidator<BulkAssignRequest>
    {
        public BulkAssignValidator()
        {
            RuleFor(request => request.AthleteIds)
                .NotEmpty().WithMessage("Hay que indicar al menos un deportista.");

            RuleFor(request => request.AthleteIds)
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .When(request => request.AthleteIds.Count > 0)
                .WithMessage("El mismo deportista está repetido en la lista.");
        }
    }

    public static IEndpointRouteBuilder MapAthleteAccreditations(this IEndpointRouteBuilder routes)
    {
        const string Base = "/competitions/{competitionId:guid}/accreditation/athletes";

        routes.MapGet(Base, ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadAthleteAccreditations")
            .WithSummary("Lists the competition's roster with each person's accreditation status.");

        routes.MapGet($"{Base}/{{athleteId:guid}}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadAthleteAccreditation")
            .WithSummary("Reads one person's accreditation category and what it resolves to.");

        routes.MapPut($"{Base}/{{athleteId:guid}}", UpsertAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("AssignAthleteAccreditation")
            .WithSummary("Assigns or changes a person's accreditation category.");

        routes.MapDelete($"{Base}/{{athleteId:guid}}", DeleteAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("DeleteAthleteAccreditation")
            .WithSummary("Removes a person's accreditation, exceptions included.");

        routes.MapPost(
                "/competitions/{competitionId:guid}/accreditation/categories/{categoryId:guid}/athletes",
                AssignCategoryBulk)
            .RequireRole(MembershipRole.Operator)
            .WithName("AssignAccreditationCategoryBulk")
            .WithSummary("Assigns one accreditation category to several people at once.");

        return routes;
    }

    /// <summary>
    /// The active roster of the competition, each row carrying its
    /// accreditation if one has been assigned.
    /// </summary>
    /// <remarks>
    /// Three queries merged in memory rather than one join, the same shape
    /// <c>AthleteReportQuery</c> already reads an athlete's registrations in:
    /// the roster, who is accredited and under what, and how many exceptions
    /// each one carries, are three independent questions with three simple
    /// answers, not one query worth contorting into a single round trip for.
    ///
    /// Withdrawn registrations are left out, matching exactly who
    /// <see cref="Documents.IssueDocumentsJob"/> would print for — someone who
    /// left the team mid-season has no reason to appear on a screen about
    /// what to print for people still on it.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid competitionId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? search = null)
    {
        if (!await database.Competitions.AnyAsync(candidate => candidate.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        search = QueryFilter.OrAbsent(search);

        var roster = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.Team!.Category!.CompetitionId == competitionId)
            .Where(entry => search == null
                || EF.Functions.ILike(entry.Athlete!.FirstName, $"%{search}%")
                || EF.Functions.ILike(entry.Athlete!.LastName, $"%{search}%"))
            .Select(entry => new
            {
                entry.AthleteId,
                entry.Athlete!.FirstName,
                entry.Athlete.LastName,
                TeamName = entry.Team!.Name,
            })
            .ToListAsync(cancellationToken);

        var accreditations = await database.AthleteAccreditations
            .AsNoTracking()
            .Where(accreditation => accreditation.CompetitionId == competitionId)
            .Select(accreditation => new
            {
                accreditation.Id,
                accreditation.AthleteId,
                accreditation.CategoryId,
                CategoryCode = accreditation.Category!.Code,
                CategoryName = accreditation.Category.Name,
            })
            .ToListAsync(cancellationToken);

        var overrideCounts = await database.AthleteAccreditationItems
            .AsNoTracking()
            .Where(exception => exception.CompetitionId == competitionId)
            .GroupBy(exception => exception.AccreditationId)
            .Select(group => new { AccreditationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.AccreditationId, group => group.Count, cancellationToken);

        var accreditationByAthlete = accreditations.ToDictionary(accreditation => accreditation.AthleteId);

        return Results.Ok(roster
            .Select(entry =>
            {
                accreditationByAthlete.TryGetValue(entry.AthleteId, out var accreditation);

                return new RosterSummary(
                    entry.AthleteId,
                    entry.FirstName,
                    entry.LastName,
                    entry.TeamName,
                    accreditation?.Id,
                    accreditation?.CategoryId,
                    accreditation?.CategoryCode,
                    accreditation?.CategoryName,
                    accreditation is null ? 0 : overrideCounts.GetValueOrDefault(accreditation.Id));
            })

            // Still-unassigned people first — the ones a screen built to close
            // this gap actually needs to surface — then alphabetically.
            .OrderBy(summary => summary.CategoryId != null)
            .ThenBy(summary => summary.LastName)
            .ThenBy(summary => summary.FirstName)
            .ToList());
    }

    private static async Task<IResult> ReadAsync(
        Guid competitionId,
        Guid athleteId,
        SportFrogDbContext database,
        AccreditationResolver resolver,
        CancellationToken cancellationToken)
    {
        var accreditation = await database.AthleteAccreditations
            .AsNoTracking()
            .Where(candidate => candidate.CompetitionId == competitionId && candidate.AthleteId == athleteId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.CategoryId,
                CategoryCode = candidate.Category!.Code,
                CategoryName = candidate.Category.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (accreditation is null)
        {
            return Results.NotFound();
        }

        var resolved = await resolver.ResolveAsync(accreditation.Id, cancellationToken);

        return Results.Ok(new DetailResponse(
            accreditation.Id, accreditation.CategoryId, accreditation.CategoryCode, accreditation.CategoryName,
            resolved));
    }

    /// <summary>
    /// Assigns or changes a person's accreditation category.
    /// </summary>
    /// <remarks>
    /// A plain upsert, unlike most assignment endpoints in this API: an
    /// operator correcting "this one is really technical staff, not an
    /// athlete" is not an error to refuse and retry as an update, it is the
    /// ordinary way this gets corrected, so re-running this with a different
    /// category is how a correction is made.
    ///
    /// A category change does not touch this person's overrides. They are
    /// recorded against the person, not against the category, and most of
    /// them stay true regardless of which one they hold — moving someone from
    /// "Deportista" to "Cuerpo técnico" should not silently discard a note
    /// that they are banned from the dining hall. An operator who wants the
    /// overrides reviewed after a category change does that explicitly,
    /// through <see cref="ManageAthleteAccreditationOverrides"/>.
    /// </remarks>
    private static async Task<IResult> UpsertAsync(
        Guid competitionId,
        Guid athleteId,
        AssignRequest request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await ValidateTargetAsync(competitionId, athleteId, request.CategoryId, database, cancellationToken)
            is { } refusal)
        {
            return refusal;
        }

        var accreditation = await database.AthleteAccreditations.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.AthleteId == athleteId,
            cancellationToken);

        if (accreditation is null)
        {
            accreditation = new AthleteAccreditation
            {
                Id = Guid.NewGuid(),
                OrgId = organization.RequireOrganizationId(),
                CompetitionId = competitionId,
                AthleteId = athleteId,
                CategoryId = request.CategoryId,
            };

            database.AthleteAccreditations.Add(accreditation);
        }
        else
        {
            accreditation.CategoryId = request.CategoryId;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // This person was accredited by another request in the same
            // instant. Reading and retrying resolves it the ordinary way: the
            // second writer updates the row the first one just created.
            return Results.Problem(
                detail: "Esta persona fue acreditada en el mismo instante por otra solicitud. " +
                        "Volvé a intentar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(new AssignResponse(accreditation.Id));
    }

    /// <summary>
    /// Removes a person's accreditation. Their exceptions go with it — they
    /// mean nothing without the accreditation they were recorded against.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        Guid competitionId,
        Guid athleteId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var accreditation = await database.AthleteAccreditations.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.AthleteId == athleteId,
            cancellationToken);

        if (accreditation is null)
        {
            return Results.NotFound();
        }

        database.AthleteAccreditations.Remove(accreditation);

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Assigns one category to several people in one step — the way
    /// accreditation actually starts: a category given to the whole roster
    /// first, a handful corrected afterwards one at a time through
    /// <see cref="UpsertAsync"/>.
    /// </summary>
    /// <remarks>
    /// An upsert for each person, unlike <c>RegisterPlayersBulk</c>'s
    /// all-or-nothing creation: re-running this over a roster that is partly
    /// accredited already is the normal second pass, not a conflict to
    /// refuse, so whoever already holds a category simply has it replaced
    /// with this one.
    /// </remarks>
    private static async Task<IResult> AssignCategoryBulk(
        Guid competitionId,
        Guid categoryId,
        BulkAssignRequest request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (!await database.AccreditationCategories.AnyAsync(
                candidate => candidate.Id == categoryId && candidate.CompetitionId == competitionId,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var rosterAthleteIds = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.Team!.Category!.CompetitionId == competitionId)
            .Where(entry => request.AthleteIds.Contains(entry.AthleteId))
            .Select(entry => entry.AthleteId)
            .ToListAsync(cancellationToken);

        var outsideRoster = request.AthleteIds.Except(rosterAthleteIds).ToList();

        if (outsideRoster.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteIds"] = [.. outsideRoster.Select(id =>
                    $"{id} no está en el plantel activo de esta competencia.")],
            });
        }

        var existing = await database.AthleteAccreditations
            .Where(candidate => candidate.CompetitionId == competitionId
                && request.AthleteIds.Contains(candidate.AthleteId))
            .ToDictionaryAsync(candidate => candidate.AthleteId, cancellationToken);

        var accreditationIds = new List<Guid>(request.AthleteIds.Count);

        foreach (var athleteId in request.AthleteIds)
        {
            if (existing.TryGetValue(athleteId, out var accreditation))
            {
                accreditation.CategoryId = categoryId;
            }
            else
            {
                accreditation = new AthleteAccreditation
                {
                    Id = Guid.NewGuid(),
                    OrgId = organization.RequireOrganizationId(),
                    CompetitionId = competitionId,
                    AthleteId = athleteId,
                    CategoryId = categoryId,
                };

                database.AthleteAccreditations.Add(accreditation);
            }

            accreditationIds.Add(accreditation.Id);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new BulkAssignResponse(accreditationIds));
    }

    /// <summary>
    /// Everything an assignment has to be true about before it is written:
    /// the category belongs to this competition, and the person is on its
    /// active roster.
    /// </summary>
    private static async Task<IResult?> ValidateTargetAsync(
        Guid competitionId,
        Guid athleteId,
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var onRoster = await database.RosterEntries
            .AsNoTracking()
            .AnyAsync(entry => entry.WithdrawnAt == null
                && entry.AthleteId == athleteId
                && entry.Team!.Category!.CompetitionId == competitionId,
                cancellationToken);

        if (!onRoster)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteId"] = ["Esta persona no está en el plantel activo de esta competencia."],
            });
        }

        var categoryBelongs = await database.AccreditationCategories
            .AsNoTracking()
            .AnyAsync(
                category => category.Id == categoryId && category.CompetitionId == competitionId,
                cancellationToken);

        if (!categoryBelongs)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["CategoryId"] = ["Ninguna categoría de acreditación de esta competencia tiene ese identificador."],
            });
        }

        return null;
    }
}
