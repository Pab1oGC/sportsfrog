using FluentValidation;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// What a caller sends to set up a competition, or to correct one.
/// </summary>
/// <remarks>
/// The sport is not here. It is a column on the table, but it is not an
/// independent fact: the ruleset is written for exactly one sport, so asking
/// the caller for both invites a request that names football and points at a
/// volleyball ruleset, and then makes this code decide which half was meant.
/// The ruleset answers it, and the column is filled from there.
///
/// The status is not here either. A competition is created in draft and moves
/// on through transitions that have their own rules; letting an edit set it
/// directly would let a competition jump from draft to finished without ever
/// having been played.
/// </remarks>
public sealed record CompetitionContract(
    Guid RulesetId,
    string Name,
    string Slug,
    string Season,
    string Format,
    string CaptureLevel,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    CompetitionSettings? Settings);

internal sealed class CompetitionContractValidator : AbstractValidator<CompetitionContract>
{
    /// <summary>
    /// Read as a string and parsed here rather than bound as an enum, which
    /// is how every other contract in this codebase takes one. An enum bound
    /// directly fails inside the JSON reader on an unrecognized value, and
    /// that failure never reaches a validator: the caller gets a body that
    /// could not be read instead of a sentence naming the field and listing
    /// what it accepts.
    /// </summary>
    public static bool TryReadCaptureLevel(string? value, out CaptureLevel level) =>
        WireEnum.TryParse(value, out level);

    /// <summary>
    /// Whether one scheduling window says enough to be scheduled against.
    /// </summary>
    private static bool IsUsableWindow(ScheduleSpace window) =>
        window.VenueSpaceId != Guid.Empty
        && (window.Days is null || window.Days.All(day => day is >= 0 and <= 6))
        && TimeOnly.TryParse(window.From, out var opens)
        && TimeOnly.TryParse(window.To, out var closes)
        && opens <= closes;

    public CompetitionContractValidator()
    {
        RuleFor(contract => contract.RulesetId)
            .NotEmpty().WithMessage("The ruleset is required.");

        RuleFor(contract => contract.Name)
            .NotEmpty().WithMessage("The competition name is required.")
            .MaximumLength(120);

        // Checked against the normalized form, since that is what gets
        // stored: an address typed in capitals is accepted and lowercased,
        // not rejected.
        RuleFor(contract => contract.Slug)
            .Must(slug => Slug.IsAcceptable(Slug.Normalize(slug)))
            .WithMessage(Slug.Requirement);

        RuleFor(contract => contract.Season)
            .NotEmpty().WithMessage("The season is required.")
            .MaximumLength(40);

        RuleFor(contract => contract.Format)
            .Must(CompetitionFormat.All.Contains)
            .WithMessage("Unknown format. Available: " +
                         $"{string.Join(", ", CompetitionFormat.All.Order(StringComparer.Ordinal))}.");

        RuleFor(contract => contract.CaptureLevel)
            .Must(level => TryReadCaptureLevel(level, out _))
            .WithMessage(
                $"Unknown capture level. Available: {WireEnum.Options<CaptureLevel>()}.");

        // The scheduling windows are checked for shape only. Whether the
        // spaces they name exist is not asked here: the venues module already
        // refuses to remove a space a competition schedules against, so the
        // reference cannot go stale, and a window naming one that was never
        // real simply places nothing — which the placement reports.
        When(contract => contract.Settings?.Schedule is not null, () =>
        {
            RuleFor(contract => contract.Settings!.Schedule!.SlotMinutes)
                .InclusiveBetween((short)1, (short)600)
                .WithMessage("A fixture occupies its space for between 1 and 600 minutes.");

            RuleFor(contract => contract.Settings!.Schedule!.Spaces)
                .Must(spaces => spaces == null || spaces.All(IsUsableWindow))
                .WithMessage("Each scheduling window needs a space, days between 0 (Sunday) and " +
                             "6, and times written as HH:mm with the first no later than the " +
                             "last.");
        });

        // Mirrors ck_competition_dates. Stated here as well so the caller is
        // told which field is wrong instead of receiving the constraint's
        // name, and only when both are given: either may be left open.
        RuleFor(contract => contract.EndsOn)
            .GreaterThanOrEqualTo(contract => contract.StartsOn!.Value)
            .When(contract => contract.StartsOn.HasValue && contract.EndsOn.HasValue)
            .WithMessage("A competition cannot end before it starts.");
    }
}
