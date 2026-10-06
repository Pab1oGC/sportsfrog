using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Accreditation;

/// <summary>
/// The accreditation categories of a competition, and the package each one
/// carries.
/// </summary>
/// <remarks>
/// A category is what makes accrediting four hundred people a matter of
/// choosing one each, rather than granting them a door at a time. It is also
/// what the card prints largest: the code in its own coloured box on the
/// front, and the same colour along the footer band of both faces.
///
/// The package is edited as a whole rather than one grant at a time, because
/// that is how somebody thinks about it — "this category opens these" is one
/// decision, and a screen that saved it as eight separate calls would be a
/// screen that can be half-saved.
/// </remarks>
public static class ManageAccreditationCategories
{
    public sealed record Request(
        string Code,
        string Name,
        string ColorHex,
        short DisplayOrder = 0);

    public sealed record Response(Guid Id);

    /// <param name="ItemIds">
    /// The whole package, replacing whatever was there. Not a list to add to:
    /// sending the set that should hold is the only form in which removing a
    /// grant is expressible at all.
    /// </param>
    public sealed record PackageRequest(IReadOnlyList<Guid> ItemIds);

    public sealed record Summary(
        Guid Id,
        string Code,
        string Name,
        string ColorHex,
        short DisplayOrder,
        IReadOnlyList<Guid> ItemIds,
        int AccreditedCount);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Code)
                .NotEmpty().WithMessage("El código es obligatorio.")
                .MaximumLength(4).WithMessage("El código entra en la caja de categoría: hasta 4 caracteres.")
                .Must(code => code is not null && code.Trim() == code && !code.Contains(' '))
                .WithMessage("El código no lleva espacios.");

            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .MaximumLength(80);

            // Written straight into a drawing instruction rather than compared
            // against anything, so it is checked here and again by the
            // database.
            RuleFor(request => request.ColorHex)
                .Must(BeAColor)
                .WithMessage("El color se escribe como #rrggbb.");

            RuleFor(request => request.DisplayOrder)
                .InclusiveBetween((short)0, (short)999);
        }

        private static bool BeAColor(string? value) =>
            value is { Length: 7 }
            && value[0] == '#'
            && value[1..].All(Uri.IsHexDigit);
    }

    internal sealed class PackageValidator : AbstractValidator<PackageRequest>
    {
        public PackageValidator() =>
            RuleFor(request => request.ItemIds)
                .NotNull()
                .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
                .WithMessage("Hay elementos repetidos en el paquete.");
    }

    public static IEndpointRouteBuilder MapAccreditationCategories(this IEndpointRouteBuilder routes)
    {
        const string Base = "/competitions/{competitionId:guid}/accreditation/categories";

        routes.MapGet(Base, ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadAccreditationCategories")
            .WithSummary("Lists a competition's accreditation categories and what each one grants.");

        routes.MapPost(Base, CreateAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("CreateAccreditationCategory")
            .WithSummary("Adds an accreditation category to a competition.");

        routes.MapPut($"{Base}/{{id:guid}}", UpdateAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("UpdateAccreditationCategory")
            .WithSummary("Corrects an accreditation category.");

        routes.MapPut($"{Base}/{{id:guid}}/items", SetPackageAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("SetAccreditationCategoryItems")
            .WithSummary("Replaces what an accreditation category grants.");

        routes.MapDelete($"{Base}/{{id:guid}}", DeleteAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("DeleteAccreditationCategory")
            .WithSummary("Removes a category nobody is accredited under.");

        return routes;
    }

    private static async Task<IResult> ListAsync(
        Guid competitionId,
        SportFrogDbContext database,
        CancellationToken cancellationToken) =>
        Results.Ok(await database.AccreditationCategories
            .AsNoTracking()
            .Where(category => category.CompetitionId == competitionId)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Code)
            .Select(category => new Summary(
                category.Id,
                category.Code,
                category.Name,
                category.ColorHex,
                category.DisplayOrder,
                database.AccreditationCategoryItems
                    .Where(link => link.CategoryId == category.Id)
                    .Select(link => link.ItemId)
                    .ToList(),

                // What makes a category deletable or not, answered before
                // somebody tries.
                database.AthleteAccreditations.Count(held => held.CategoryId == category.Id)))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> CreateAsync(
        Guid competitionId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (!await database.Competitions
                .AnyAsync(candidate => candidate.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        var category = new AccreditationCategory
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competitionId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ColorHex = request.ColorHex,
            DisplayOrder = request.DisplayOrder,
        };

        database.AccreditationCategories.Add(category);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Duplicate(category.Code);
        }

        return Results.Created(
            $"/competitions/{competitionId}/accreditation/categories/{category.Id}",
            new Response(category.Id));
    }

    private static async Task<IResult> UpdateAsync(
        Guid competitionId,
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var category = await database.AccreditationCategories.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        category.Code = request.Code.Trim();
        category.Name = request.Name.Trim();
        category.ColorHex = request.ColorHex;
        category.DisplayOrder = request.DisplayOrder;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Duplicate(category.Code);
        }

        return Results.NoContent();
    }

    /// <summary>
    /// Replaces what a category grants, in one transaction.
    /// </summary>
    /// <remarks>
    /// Every item is checked against this competition before anything is
    /// written. The database would refuse a foreign one anyway — both ends of
    /// the link name the competition for exactly that reason — but a composite
    /// foreign key violation is not an answer anybody can act on, and the
    /// refusal should name what was wrong with the request.
    /// </remarks>
    private static async Task<IResult> SetPackageAsync(
        Guid competitionId,
        Guid id,
        PackageRequest request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var category = await database.AccreditationCategories.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var wanted = request.ItemIds.Distinct().ToList();

        var belonging = await database.AccreditationItems
            .Where(item => item.CompetitionId == competitionId && wanted.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (belonging.Count != wanted.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["itemIds"] = ["Hay elementos que no son del catálogo de esta competencia."],
            });
        }

        var current = await database.AccreditationCategoryItems
            .Where(link => link.CategoryId == id)
            .ToListAsync(cancellationToken);

        database.AccreditationCategoryItems.RemoveRange(
            current.Where(link => !wanted.Contains(link.ItemId)));

        var held = current.Select(link => link.ItemId).ToHashSet();

        foreach (var itemId in wanted.Where(candidate => !held.Contains(candidate)))
        {
            database.AccreditationCategoryItems.Add(new AccreditationCategoryItem
            {
                OrgId = organization.RequireOrganizationId(),
                CompetitionId = competitionId,
                CategoryId = id,
                ItemId = itemId,
            });
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Removes a category nobody holds.
    /// </summary>
    /// <remarks>
    /// The schema restricts this rather than cascading, and the check here is
    /// so the answer says who is in the way instead of being a foreign key
    /// error. Deleting a category people are accredited under would strip
    /// them of the thing their card is printed from.
    /// </remarks>
    private static async Task<IResult> DeleteAsync(
        Guid competitionId,
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var category = await database.AccreditationCategories.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var accredited = await database.AthleteAccreditations
            .CountAsync(held => held.CategoryId == id, cancellationToken);

        if (accredited > 0)
        {
            return Results.Problem(
                detail: $"No se puede eliminar: {accredited} persona(s) están acreditadas en "
                        + "esta categoría. Movelas a otra primero.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Its package goes with it, which is the cascade doing what it is for:
        // the grants of a category that no longer exists mean nothing.
        database.AccreditationCategories.Remove(category);

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IResult Duplicate(string code) =>
        Results.Problem(
            detail: $"Ya hay una categoría con el código '{code}' en esta competencia.",
            statusCode: StatusCodes.Status409Conflict);
}
