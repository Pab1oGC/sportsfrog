using FluentValidation;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Who a category admits, and under what rules.
/// </summary>
/// <remarks>
/// The competition is not here: it is the route the category hangs off, and a
/// body that could name a different one would let an edit move a category
/// between competitions, which is not a correction but a different category.
/// </remarks>
public sealed record CategoryContract(
    Guid? RulesetId,
    string Name,
    string? Gender,
    DateOnly? BirthDateFrom,
    DateOnly? BirthDateTo,
    short? MaxRosterSize,
    short DisplayOrder,
    short? QualifiersPerGroup,
    decimal? MinWeightKg = null,
    decimal? MaxWeightKg = null,
    bool UsesRepechage = false);

internal sealed class CategoryContractValidator : AbstractValidator<CategoryContract>
{
    /// <summary>
    /// A roster of a hundred is not a squad, it is a mistyped number. The
    /// ceiling exists to catch that and nothing else — the real limit is
    /// whatever the organizers set below it.
    /// </summary>
    private const short MaximumRosterSize = 60;

    public CategoryContractValidator()
    {
        RuleFor(contract => contract.Name)
            .NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
            .MaximumLength(80);

        RuleFor(contract => contract.Gender)
            .Must(gender => Sex.IsAcceptable(gender))
            .When(contract => contract.Gender is not null)
            .WithMessage("La categoría admite F o M, o se deja sin definir para admitir a cualquiera.");

        // Read as the window of birth dates the category admits: the earliest
        // is its oldest player, the latest its youngest. Either end may be
        // left open — an adult category usually caps nothing at the top.
        RuleFor(contract => contract.BirthDateTo)
            .GreaterThanOrEqualTo(contract => contract.BirthDateFrom!.Value)
            .When(contract => contract.BirthDateFrom.HasValue && contract.BirthDateTo.HasValue)
            .WithMessage("La fecha de nacimiento más antigua admitida no puede ser posterior a la " +
                         "más reciente: tal como está, nadie podría calificar nunca.");

        RuleFor(contract => contract.MaxRosterSize)
            .InclusiveBetween((short)1, MaximumRosterSize)
            .When(contract => contract.MaxRosterSize.HasValue)
            .WithMessage($"Una nómina tiene entre 1 y {MaximumRosterSize} jugadores, o se deja " +
                         "sin definir para no ponerle límite.");

        RuleFor(contract => contract.DisplayOrder)
            .InclusiveBetween((short)0, (short)999);

        // A group of one team is not a group, and nothing sends more than a
        // handful forward — the ceiling exists for the same reason the
        // roster one does, to catch a mistyped number rather than to
        // restrict a real tournament.
        RuleFor(contract => contract.QualifiersPerGroup)
            .InclusiveBetween((short)1, (short)16)
            .When(contract => contract.QualifiersPerGroup.HasValue)
            .WithMessage("La cantidad de clasificados por grupo debe estar entre 1 y 16, o se " +
                         "deja sin definir para no resaltar ninguna fila.");

        // Mirrors ck_categories_min_weight_positive / ck_categories_max_weight_positive.
        RuleFor(contract => contract.MinWeightKg)
            .GreaterThan(0)
            .When(contract => contract.MinWeightKg.HasValue)
            .WithMessage("El peso mínimo tiene que ser mayor que cero.");

        RuleFor(contract => contract.MaxWeightKg)
            .GreaterThan(0)
            .When(contract => contract.MaxWeightKg.HasValue)
            .WithMessage("El peso máximo tiene que ser mayor que cero.");

        // Read as the window of weight the category admits, same shape as
        // the birth-date window above. Mirrors ck_categories_weight_range_valid.
        RuleFor(contract => contract.MaxWeightKg)
            .GreaterThanOrEqualTo(contract => contract.MinWeightKg!.Value)
            .When(contract => contract.MinWeightKg.HasValue && contract.MaxWeightKg.HasValue)
            .WithMessage("El peso mínimo admitido no puede ser mayor que el máximo: tal como " +
                         "está, nadie podría calificar nunca.");
    }
}

