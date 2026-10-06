using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Accreditation;

/// <summary>
/// What one person has beyond, or short of, what their accreditation category
/// carries.
/// </summary>
/// <remarks>
/// Replaced as a whole, the same way <see cref="ManageAccreditationCategories"/>
/// saves a category's package: an operator deciding "this one also gets
/// transport, but not the dining hall" is one decision about the person, and
/// a screen that saved it one exception at a time would be a screen that can
/// be half-saved.
/// </remarks>
public static class ManageAthleteAccreditationOverrides
{
    public sealed record Override(Guid ItemId, bool Granted);

    /// <param name="Overrides">
    /// The whole set of exceptions this person should have, replacing
    /// whatever was there. An empty list means this person now gets exactly
    /// their category's package — removing every exception is expressed by
    /// sending none, not by a separate call per exception.
    /// </param>
    public sealed record Request(IReadOnlyList<Override> Overrides);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Overrides)
                .NotNull()
                .Must(overrides => overrides.Select(entry => entry.ItemId).Distinct().Count() == overrides.Count)
                .WithMessage("Hay elementos repetidos entre las excepciones.");
    }

    public static IEndpointRouteBuilder MapAthleteAccreditationOverrides(this IEndpointRouteBuilder routes)
    {
        routes.MapPut(
                "/competitions/{competitionId:guid}/accreditation/athletes/{athleteId:guid}/overrides",
                SetAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("SetAthleteAccreditationOverrides")
            .WithSummary("Replaces what one person has beyond, or short of, their category's package.");

        return routes;
    }

    /// <summary>
    /// Replaces a person's exceptions, in one transaction.
    /// </summary>
    /// <remarks>
    /// Every item named is checked against this competition's catalogue
    /// first, the same precaution <see cref="ManageAccreditationCategories.SetPackageAsync"/>
    /// takes for a category's package and for the same reason: the database
    /// would refuse a foreign one through the composite foreign key anyway,
    /// but a constraint violation is not an answer an operator can act on.
    /// </remarks>
    private static async Task<IResult> SetAsync(
        Guid competitionId,
        Guid athleteId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var accreditation = await database.AthleteAccreditations.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.AthleteId == athleteId,
            cancellationToken);

        if (accreditation is null)
        {
            return Results.NotFound();
        }

        var wanted = request.Overrides
            .GroupBy(entry => entry.ItemId)
            .ToDictionary(group => group.Key, group => group.First().Granted);

        var belonging = await database.AccreditationItems
            .Where(item => item.CompetitionId == competitionId && wanted.Keys.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (belonging.Count != wanted.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Overrides"] = ["Hay elementos que no son del catálogo de esta competencia."],
            });
        }

        var current = await database.AthleteAccreditationItems
            .Where(exception => exception.AccreditationId == accreditation.Id)
            .ToListAsync(cancellationToken);

        database.AthleteAccreditationItems.RemoveRange(
            current.Where(exception => !wanted.ContainsKey(exception.ItemId)));

        foreach (var exception in current.Where(exception => wanted.ContainsKey(exception.ItemId)))
        {
            exception.Granted = wanted[exception.ItemId];
        }

        var held = current.Select(exception => exception.ItemId).ToHashSet();

        foreach (var (itemId, granted) in wanted.Where(entry => !held.Contains(entry.Key)))
        {
            database.AthleteAccreditationItems.Add(new AthleteAccreditationItem
            {
                OrgId = organization.RequireOrganizationId(),
                CompetitionId = competitionId,
                AccreditationId = accreditation.Id,
                ItemId = itemId,
                Granted = granted,
            });
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
