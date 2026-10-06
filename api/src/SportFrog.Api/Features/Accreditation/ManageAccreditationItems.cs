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
/// The codes a competition's credentials can carry: the discipline, the
/// venues, the services and the access zones.
/// </summary>
/// <remarks>
/// One slice for all four operations, like the venues it resembles. An item is
/// a code, a name and a position; four files would spread forty lines across
/// four headers and bury the delete rule, which is the only part with anything
/// to say.
///
/// Scoped under a competition rather than the organization, because that is
/// what the catalogue is: one set of doors and services per competition, not a
/// vocabulary the whole league shares. Two competitions of the same
/// organization can both have a zone called <c>4</c> meaning different places.
/// </remarks>
public static class ManageAccreditationItems
{
    /// <param name="Kind">
    /// Arrives as text and is parsed here, like every other enum on this API:
    /// an unrecognised value comes back as a refusal naming the options, not
    /// as a failure to read the request at all.
    /// </param>
    public sealed record Request(
        string Kind,
        string Code,
        string Name,
        string? ColorHex = null,
        string? IconKey = null,
        short DisplayOrder = 0);

    public sealed record Response(Guid Id);

    public sealed record Summary(
        Guid Id,
        AccreditationItemKind Kind,
        string Code,
        string Name,
        string? ColorHex,
        string? IconKey,
        short DisplayOrder,
        int CategoryCount);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Kind)
                .Must(kind => WireEnum.TryParse<AccreditationItemKind>(kind, out _))
                .WithMessage($"Tipo desconocido. Disponibles: {WireEnum.Options<AccreditationItemKind>()}.");

            // Four characters, and that is about the card rather than about
            // tidiness: the box this is printed in is a few millimetres wide,
            // so a longer code is not a request to refuse politely later, it
            // is a card that comes out wrong.
            RuleFor(request => request.Code)
                .NotEmpty().WithMessage("El código es obligatorio.")
                .MaximumLength(4).WithMessage("El código entra en una caja de pocos milímetros: hasta 4 caracteres.")
                .Must(code => code is not null && code.Trim() == code && !code.Contains(' '))
                .WithMessage("El código no lleva espacios.");

            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .MaximumLength(80);

            // Written straight into a drawing instruction, like the one on
            // accreditation categories — checked here too rather than trusted.
            RuleFor(request => request.ColorHex)
                .Must(BeAColor)
                .WithMessage("El color se escribe como #rrggbb.")
                .When(request => !string.IsNullOrWhiteSpace(request.ColorHex));

            RuleFor(request => request.DisplayOrder)
                .InclusiveBetween((short)0, (short)999);
        }

        private static bool BeAColor(string? value) =>
            value is { Length: 7 }
            && value[0] == '#'
            && value[1..].All(Uri.IsHexDigit);
    }

    public static IEndpointRouteBuilder MapAccreditationItems(this IEndpointRouteBuilder routes)
    {
        const string Base = "/competitions/{competitionId:guid}/accreditation/items";

        routes.MapGet(Base, ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadAccreditationItems")
            .WithSummary("Lists what a competition's credentials can carry.");

        routes.MapPost(Base, CreateAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("CreateAccreditationItem")
            .WithSummary("Adds a discipline, venue, service or access zone to a competition.");

        routes.MapPut($"{Base}/{{id:guid}}", UpdateAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("UpdateAccreditationItem")
            .WithSummary("Corrects one entry of a competition's accreditation catalogue.");

        routes.MapDelete($"{Base}/{{id:guid}}", DeleteAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName("DeleteAccreditationItem")
            .WithSummary("Removes an entry nothing grants.");

        return routes;
    }

    /// <summary>
    /// The catalogue, in printing order.
    /// </summary>
    /// <remarks>
    /// Ordered by kind and then by position, which is the order the card draws
    /// them in — the discipline opens the first row, the venues finish it, the
    /// services fill the second, the zones run along the foot. A list sorted
    /// alphabetically would be a list nobody could check against a card.
    ///
    /// The count of categories granting each one comes along because it is the
    /// answer to the only question asked before deleting one.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid competitionId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? kind = null)
    {
        if (kind is not null && !WireEnum.TryParse<AccreditationItemKind>(kind, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["kind"] = [$"Tipo desconocido. Disponibles: {WireEnum.Options<AccreditationItemKind>()}."],
            });
        }

        var wanted = kind is null ? (AccreditationItemKind?)null : WireEnum.Parse<AccreditationItemKind>(kind);

        return Results.Ok(await database.AccreditationItems
            .AsNoTracking()
            .Where(item => item.CompetitionId == competitionId)
            .Where(item => wanted == null || item.Kind == wanted)
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.Code)
            .Select(item => new Summary(
                item.Id,
                item.Kind,
                item.Code,
                item.Name,
                item.ColorHex,
                item.IconKey,
                item.DisplayOrder,
                database.AccreditationCategoryItems.Count(link => link.ItemId == item.Id)))
            .ToListAsync(cancellationToken));
    }

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

        var kind = WireEnum.Parse<AccreditationItemKind>(request.Kind);
        var code = request.Code.Trim();

        // One discipline, because the card has one box for it and a
        // competition is played in one sport. Caught here rather than left to
        // the operator to notice, since the second one would simply never be
        // printed.
        if (kind == AccreditationItemKind.Discipline
            && await database.AccreditationItems.AnyAsync(
                item => item.CompetitionId == competitionId
                    && item.Kind == AccreditationItemKind.Discipline,
                cancellationToken))
        {
            return Results.Problem(
                detail: "Esta competencia ya tiene su disciplina. La credencial imprime una sola; "
                        + "corregí la que existe en lugar de agregar otra.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var item = new AccreditationItem
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competitionId,
            Kind = kind,
            Code = code,
            Name = request.Name.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex,
            IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? null : request.IconKey.Trim(),
            DisplayOrder = request.DisplayOrder,
        };

        database.AccreditationItems.Add(item);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Duplicate(kind, code);
        }

        return Results.Created(
            $"/competitions/{competitionId}/accreditation/items/{item.Id}", new Response(item.Id));
    }

    private static async Task<IResult> UpdateAsync(
        Guid competitionId,
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var item = await database.AccreditationItems.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId,
            cancellationToken);

        if (item is null)
        {
            return Results.NotFound();
        }

        var kind = WireEnum.Parse<AccreditationItemKind>(request.Kind);

        // The kind decides which row of the card draws it, so changing it
        // moves the box somewhere else. Allowed — somebody who filed the
        // village as a service should be able to correct that — but not into
        // a second discipline.
        if (kind == AccreditationItemKind.Discipline
            && item.Kind != AccreditationItemKind.Discipline
            && await database.AccreditationItems.AnyAsync(
                candidate => candidate.CompetitionId == competitionId
                    && candidate.Kind == AccreditationItemKind.Discipline,
                cancellationToken))
        {
            return Results.Problem(
                detail: "Esta competencia ya tiene su disciplina. La credencial imprime una sola.",
                statusCode: StatusCodes.Status409Conflict);
        }

        item.Kind = kind;
        item.Code = request.Code.Trim();
        item.Name = request.Name.Trim();
        item.ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex;
        item.IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? null : request.IconKey.Trim();
        item.DisplayOrder = request.DisplayOrder;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Duplicate(item.Kind, item.Code);
        }

        return Results.NoContent();
    }

    /// <summary>
    /// Removes an entry, if nothing grants it.
    /// </summary>
    /// <remarks>
    /// The schema would cascade this: deleting a zone would quietly take it
    /// out of every category that granted it and out of every exception
    /// recorded against a person. That is the right shape for the database and
    /// the wrong answer for an operator, who would be told the row was deleted
    /// and not that nine categories just lost a door. So it is refused while
    /// anything points at it, and the cascade stays as the backstop it should
    /// be rather than the behaviour anybody relies on.
    /// </remarks>
    private static async Task<IResult> DeleteAsync(
        Guid competitionId,
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var item = await database.AccreditationItems.SingleOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CompetitionId == competitionId,
            cancellationToken);

        if (item is null)
        {
            return Results.NotFound();
        }

        var granting = await database.AccreditationCategoryItems
            .CountAsync(link => link.ItemId == id, cancellationToken);

        if (granting > 0)
        {
            return Results.Problem(
                detail: $"No se puede eliminar: {granting} categoría(s) lo otorgan. "
                        + "Quitalo de esas categorías primero.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var excepted = await database.AthleteAccreditationItems
            .CountAsync(link => link.ItemId == id, cancellationToken);

        if (excepted > 0)
        {
            return Results.Problem(
                detail: $"No se puede eliminar: hay {excepted} excepción(es) de personas que lo nombran.",
                statusCode: StatusCodes.Status409Conflict);
        }

        database.AccreditationItems.Remove(item);

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IResult Duplicate(AccreditationItemKind kind, string code) =>
        Results.Problem(
            detail: $"Ya hay un elemento de tipo {WireEnum.Label(kind)} con el código '{code}' "
                    + "en esta competencia.",
            statusCode: StatusCodes.Status409Conflict);
}
