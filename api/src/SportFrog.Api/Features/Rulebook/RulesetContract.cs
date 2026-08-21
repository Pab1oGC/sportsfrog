using FluentValidation;
using FluentValidation.Results;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// What a caller sends to write down a ruleset, whether it is new or a
/// correction of one that exists.
/// </summary>
/// <remarks>
/// One record for both, rather than one per endpoint as the club and athlete
/// slices have. There the two differ — editing a club can deactivate it,
/// creating one cannot — and separate records say so. Here they do not
/// differ at all: the same document describes the ruleset either way, and
/// the only distinction is whether it lands on a new row. Two identical
/// records with two identical validators would drift, and the drift would be
/// a rule enforced on creation and quietly lost on edit.
///
/// The sport travels on an edit too, and is not optional there. It is the
/// caller stating which sport they believe they are editing: a ruleset never
/// changes sport — its whole configuration is written against one — so a
/// value that disagrees with the stored one is a request built on a stale
/// idea of the resource, and is refused rather than applied.
/// </remarks>
public sealed record RulesetContract(string SportCode, string Name, RulesetConfiguration Config);

/// <summary>
/// Everything a ruleset document has to satisfy, in one place because both
/// endpoints ask for the same thing.
/// </summary>
internal sealed class RulesetContractValidator : AbstractValidator<RulesetContract>
{
    public RulesetContractValidator(RulesetPolicy policy)
    {
        RuleFor(contract => contract.SportCode)
            .NotEmpty().WithMessage("The sport is required.")
            .MaximumLength(50);

        RuleFor(contract => contract.Name)
            .NotEmpty().WithMessage("The ruleset name is required.")
            .MaximumLength(120);

        RuleFor(contract => contract.Config)
            .NotNull().WithMessage("The configuration is required.")
            .SetValidator(new RulesetShapeValidator());

        // Held back until the structure is sound, so a configuration missing
        // its periods is reported as missing them rather than as pricing the
        // wrong outcomes for a number of sets it never gave.
        RuleFor(contract => contract)
            .CustomAsync(async (contract, context, cancellationToken) =>
            {
                var violations = await policy.InspectAsync(
                    contract.SportCode, contract.Config, cancellationToken);

                foreach (var violation in violations)
                {
                    context.AddFailure(new ValidationFailure(violation.Property, violation.Message));
                }
            })
            .When(contract => contract.SportCode is { Length: > 0 }
                && contract.Config is { Periods: not null, Points: not null, Tiebreakers: not null });
    }
}
