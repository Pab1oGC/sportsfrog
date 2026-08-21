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
    short DisplayOrder);

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
            .NotEmpty().WithMessage("The category name is required.")
            .MaximumLength(80);

        RuleFor(contract => contract.Gender)
            .Must(gender => Sex.IsAcceptable(gender))
            .When(contract => contract.Gender is not null)
            .WithMessage("The category is open to F or M, or left unset to admit anyone.");

        // Read as the window of birth dates the category admits: the earliest
        // is its oldest player, the latest its youngest. Either end may be
        // left open — an adult category usually caps nothing at the top.
        RuleFor(contract => contract.BirthDateTo)
            .GreaterThanOrEqualTo(contract => contract.BirthDateFrom!.Value)
            .When(contract => contract.BirthDateFrom.HasValue && contract.BirthDateTo.HasValue)
            .WithMessage("The oldest birth date admitted cannot be later than the youngest: " +
                         "as written, no one could ever qualify.");

        RuleFor(contract => contract.MaxRosterSize)
            .InclusiveBetween((short)1, MaximumRosterSize)
            .When(contract => contract.MaxRosterSize.HasValue)
            .WithMessage($"A roster holds between 1 and {MaximumRosterSize} players, or is left " +
                         "unset to leave it uncapped.");

        RuleFor(contract => contract.DisplayOrder)
            .InclusiveBetween((short)0, (short)999);
    }
}

