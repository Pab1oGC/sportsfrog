using FluentValidation;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// The bulletin's free text, on its own.
/// </summary>
/// <remarks>
/// Split out of <see cref="CompetitionContractValidator"/> for the same
/// reason as <see cref="PublicSettingsValidator"/>: <see cref="Features.Competitions.UpdateCompetitionBulletinSettings"/>
/// needs the exact same rule, and composing it with <c>SetValidator</c>
/// keeps the two from drifting apart the way two copies of the same limits
/// eventually would. Long limits, not short ones: sanctions and general
/// provisions are often a page or two of actual regulation text, copied in
/// from wherever the federation keeps it.
/// </remarks>
public sealed class BulletinSettingsValidator : AbstractValidator<BulletinSettings>
{
    public BulletinSettingsValidator()
    {
        RuleFor(settings => settings.Introduction).MaximumLength(4000);
        RuleFor(settings => settings.Sanctions).MaximumLength(4000);
        RuleFor(settings => settings.GeneralProvisions).MaximumLength(4000);
        RuleFor(settings => settings.ContactInfo).MaximumLength(1000);
    }
}
