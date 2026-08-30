using FluentValidation;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
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
            .NotEmpty().WithMessage("El reglamento es obligatorio.");

        RuleFor(contract => contract.Name)
            .NotEmpty().WithMessage("El nombre de la competencia es obligatorio.")
            .MaximumLength(120);

        // Checked against the normalized form, since that is what gets
        // stored: an address typed in capitals is accepted and lowercased,
        // not rejected.
        RuleFor(contract => contract.Slug)
            .Must(slug => Slug.IsAcceptable(Slug.Normalize(slug)))
            .WithMessage(Slug.Requirement);

        RuleFor(contract => contract.Season)
            .NotEmpty().WithMessage("La temporada es obligatoria.")
            .MaximumLength(40);

        RuleFor(contract => contract.Format)
            .Must(CompetitionFormat.All.Contains)
            .WithMessage("Formato desconocido. Disponibles: " +
                         $"{string.Join(", ", CompetitionFormat.All.Order(StringComparer.Ordinal))}.");

        RuleFor(contract => contract.CaptureLevel)
            .Must(level => TryReadCaptureLevel(level, out _))
            .WithMessage(
                $"Nivel de captura desconocido. Disponibles: {WireEnum.Options<CaptureLevel>()}.");

        // The scheduling windows are checked for shape only. Whether the
        // spaces they name exist is not asked here: the venues module already
        // refuses to remove a space a competition schedules against, so the
        // reference cannot go stale, and a window naming one that was never
        // real simply places nothing — which the placement reports.
        When(contract => contract.Settings?.Schedule is not null, () =>
        {
            RuleFor(contract => contract.Settings!.Schedule!.SlotMinutes)
                .InclusiveBetween((short)1, (short)600)
                .WithMessage("Un partido ocupa su espacio entre 1 y 600 minutos.");

            RuleFor(contract => contract.Settings!.Schedule!.Spaces)
                .Must(spaces => spaces == null || spaces.All(IsUsableWindow))
                .WithMessage("Cada ventana de horario necesita un espacio, días entre 0 " +
                             "(domingo) y 6, y horas escritas como HH:mm, con la primera no " +
                             "posterior a la última.");
        });

        // Mirrors ck_competition_dates. Stated here as well so the caller is
        // told which field is wrong instead of receiving the constraint's
        // name, and only when both are given: either may be left open.
        RuleFor(contract => contract.EndsOn)
            .GreaterThanOrEqualTo(contract => contract.StartsOn!.Value)
            .When(contract => contract.StartsOn.HasValue && contract.EndsOn.HasValue)
            .WithMessage("Una competencia no puede terminar antes de empezar.");

        // Customization of the public page. Every field here is optional —
        // a competition that never opens this settles for the plain page it
        // always had — so everything below only runs When it was touched.
        When(contract => contract.Settings?.Public is not null, () =>
        {
            // A picture field arrives one of two ways: a data URL, freshly
            // picked and waiting to be uploaded, or a key this store already
            // handed out, sent back unchanged because nothing about it
            // changed. IsAcceptable alone would refuse the second shape —
            // it does not look like an image, it looks like the key of one.
            RuleFor(contract => contract.Settings!.Public!.BannerKey)
                .Must(value => PortalPicture.IsStoredKey(value!) || InlinePhoto.IsAcceptable(value))
                .When(contract => contract.Settings!.Public!.BannerKey is not null)
                .WithMessage(InlinePhoto.Requirement);

            RuleFor(contract => contract.Settings!.Public!.AccentColor)
                .Matches("^#[0-9a-fA-F]{6}$")
                .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.AccentColor))
                .WithMessage("El color se escribe como #rrggbb.");

            RuleFor(contract => contract.Settings!.Public!.Description)
                .MaximumLength(500)
                .WithMessage("La presentación tiene como máximo 500 caracteres.");

            RuleFor(contract => contract.Settings!.Public!.Instagram).MaximumLength(200);
            RuleFor(contract => contract.Settings!.Public!.Facebook).MaximumLength(200);
            RuleFor(contract => contract.Settings!.Public!.WhatsApp).MaximumLength(200);
            RuleFor(contract => contract.Settings!.Public!.Website).MaximumLength(200);

            RuleFor(contract => contract.Settings!.Public!.Sponsors)
                .Must(sponsors => sponsors == null || sponsors.Count <= MaximumSponsors)
                .WithMessage($"Como máximo {MaximumSponsors} auspiciantes.");

            // Guarded rather than folded into the Must above with ?? []: an
            // empty fallback is a fresh collection expression, and RuleForEach
            // cannot infer which property it is validating from one — it
            // needs the real property access underneath, which only shows up
            // once null is ruled out here instead of inside the expression.
            When(contract => contract.Settings!.Public!.Sponsors is { Count: > 0 }, () =>
            {
                RuleForEach(contract => contract.Settings!.Public!.Sponsors!)
                    .ChildRules(sponsor =>
                    {
                        sponsor.RuleFor(s => s.LogoKey)
                            .NotEmpty().WithMessage("Cada auspiciante necesita un logo.")
                            .Must(value => PortalPicture.IsStoredKey(value) || InlinePhoto.IsAcceptable(value))
                            .When(s => !string.IsNullOrEmpty(s.LogoKey))
                            .WithMessage(InlinePhoto.Requirement);

                        sponsor.RuleFor(s => s.Name)
                            .MaximumLength(80)
                            .WithMessage("El nombre del auspiciante tiene como máximo 80 caracteres.");

                        sponsor.RuleFor(s => s.Url)
                            .MaximumLength(300);
                    });
            });
        });
    }

    /// <summary>
    /// Enough to sponsor a competition without the strip turning into
    /// something a phone has to scroll sideways to read.
    /// </summary>
    private const int MaximumSponsors = 16;
}
