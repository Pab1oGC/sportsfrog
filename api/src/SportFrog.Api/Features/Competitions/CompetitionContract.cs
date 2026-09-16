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

        // The enabled pitches are checked for shape only. Whether the spaces
        // they name exist is not asked here: the venues module already
        // refuses to remove a space a competition schedules against, so the
        // reference cannot go stale, and one naming a space that was never
        // real simply places nothing — which the placement reports.
        When(contract => contract.Settings?.Schedule is not null, () =>
        {
            RuleFor(contract => contract.Settings!.Schedule!.BufferMinutes)
                .InclusiveBetween((short)0, (short)120)
                .When(contract => contract.Settings!.Schedule!.BufferMinutes.HasValue)
                .WithMessage("El margen entre partidos va de 0 a 120 minutos.");

            RuleFor(contract => contract.Settings!.Schedule!.SpaceIds)
                .Must(spaceIds => spaceIds == null || spaceIds.All(id => id != Guid.Empty))
                .WithMessage("Cada cancha habilitada necesita un espacio válido.");
        });

        // The bulletin's free text. Long limits, not short ones: sanctions
        // and general provisions are often a page or two of actual
        // regulation text, copied in from wherever the federation keeps it.
        When(contract => contract.Settings?.Bulletin is not null, () =>
        {
            RuleFor(contract => contract.Settings!.Bulletin!.Introduction).MaximumLength(4000);
            RuleFor(contract => contract.Settings!.Bulletin!.Sanctions).MaximumLength(4000);
            RuleFor(contract => contract.Settings!.Bulletin!.GeneralProvisions).MaximumLength(4000);
            RuleFor(contract => contract.Settings!.Bulletin!.ContactInfo).MaximumLength(1000);
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

            RuleFor(contract => contract.Settings!.Public!.LogoKey)
                .Must(value => PortalPicture.IsStoredKey(value!) || InlinePhoto.IsAcceptable(value))
                .When(contract => contract.Settings!.Public!.LogoKey is not null)
                .WithMessage(InlinePhoto.Requirement);

            RuleFor(contract => contract.Settings!.Public!.AccentColor)
                .Matches(HexColour)
                .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.AccentColor))
                .WithMessage("El color se escribe como #rrggbb.");

            // The portal theme. Every field optional — a theme that only
            // named a font is valid — so each rule only runs When that one
            // field was given. The four colours share the accent colour's
            // pattern; the four choice fields are matched against the same
            // allow-lists the public page defaults from, so a stale client
            // sending a value that was later renamed is told the options
            // rather than silently ignored.
            When(contract => contract.Settings!.Public!.Theme is not null, () =>
            {
                RuleFor(contract => contract.Settings!.Public!.Theme!.Primary)
                    .Matches(HexColour)
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.Primary))
                    .WithMessage("El color principal se escribe como #rrggbb.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.PrimaryContrast)
                    .Matches(HexColour)
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.PrimaryContrast))
                    .WithMessage("El color del texto sobre el principal se escribe como #rrggbb.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.Secondary)
                    .Matches(HexColour)
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.Secondary))
                    .WithMessage("El color secundario se escribe como #rrggbb.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.Surface)
                    .Matches(HexColour)
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.Surface))
                    .WithMessage("El color de fondo se escribe como #rrggbb.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.HeadingFont)
                    .Must(value => PortalTheme.Fonts.Contains(value!))
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.HeadingFont))
                    .WithMessage($"Tipografía desconocida. Disponibles: {string.Join(", ", PortalTheme.Fonts.Order(StringComparer.Ordinal))}.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.Corners)
                    .Must(value => PortalTheme.CornerStyles.Contains(value!))
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.Corners))
                    .WithMessage($"Estilo de bordes desconocido. Disponibles: {string.Join(", ", PortalTheme.CornerStyles.Order(StringComparer.Ordinal))}.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.HeroStyle)
                    .Must(value => PortalTheme.HeroStyles.Contains(value!))
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.HeroStyle))
                    .WithMessage($"Estilo de portada desconocido. Disponibles: {string.Join(", ", PortalTheme.HeroStyles.Order(StringComparer.Ordinal))}.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.ColorScheme)
                    .Must(value => PortalTheme.ColorSchemes.Contains(value!))
                    .When(contract => !string.IsNullOrEmpty(contract.Settings!.Public!.Theme!.ColorScheme))
                    .WithMessage($"Modo de color desconocido. Disponibles: {string.Join(", ", PortalTheme.ColorSchemes.Order(StringComparer.Ordinal))}.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.FocusX)
                    .InclusiveBetween(0, 100)
                    .When(contract => contract.Settings!.Public!.Theme!.FocusX is not null)
                    .WithMessage("El punto focal horizontal va de 0 a 100.");

                RuleFor(contract => contract.Settings!.Public!.Theme!.FocusY)
                    .InclusiveBetween(0, 100)
                    .When(contract => contract.Settings!.Public!.Theme!.FocusY is not null)
                    .WithMessage("El punto focal vertical va de 0 a 100.");
            });

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

            RuleFor(contract => contract.Settings!.Public!.Gallery)
                .Must(gallery => gallery == null || gallery.Count <= MaximumGalleryPhotos)
                .WithMessage($"Como máximo {MaximumGalleryPhotos} fotos.");

            // Mismo motivo que el guard de Sponsors de arriba: RuleForEach
            // necesita la propiedad real, no un ?? [] de por medio.
            When(contract => contract.Settings!.Public!.Gallery is { Count: > 0 }, () =>
            {
                RuleForEach(contract => contract.Settings!.Public!.Gallery!)
                    .ChildRules(photo =>
                    {
                        photo.RuleFor(p => p.Key)
                            .NotEmpty().WithMessage("Cada foto necesita una imagen.")
                            .Must(value => PortalPicture.IsStoredKey(value) || InlinePhoto.IsAcceptable(value))
                            .When(p => !string.IsNullOrEmpty(p.Key))
                            .WithMessage(InlinePhoto.Requirement);

                        photo.RuleFor(p => p.Caption)
                            .MaximumLength(140)
                            .WithMessage("El epígrafe de una foto tiene como máximo 140 caracteres.");
                    });
            });

            // El orden y los nombres de las secciones. Cada fila que el
            // estudio manda ya nombra una clave real -- las cuatro que
            // PortalSection.Keys conoce -- así que lo único que puede fallar
            // es un cliente desactualizado nombrando una que ya no existe, o
            // una futura que todavía no.
            RuleFor(contract => contract.Settings!.Public!.SectionOrder)
                .Must(sections => sections == null || sections.Count <= PortalSection.Keys.Count)
                .WithMessage($"Como máximo {PortalSection.Keys.Count} secciones.");

            When(contract => contract.Settings!.Public!.SectionOrder is { Count: > 0 }, () =>
            {
                RuleForEach(contract => contract.Settings!.Public!.SectionOrder!)
                    .ChildRules(section =>
                    {
                        section.RuleFor(s => s.Key)
                            .Must(key => PortalSection.Keys.Contains(key))
                            .WithMessage($"Sección desconocida. Disponibles: {string.Join(", ", PortalSection.Keys.Order(StringComparer.Ordinal))}.");

                        section.RuleFor(s => s.Label)
                            .MaximumLength(40)
                            .WithMessage("El nombre de una sección tiene como máximo 40 caracteres.");
                    });
            });
        });
    }

    /// <summary>
    /// Enough to sponsor a competition without the strip turning into
    /// something a phone has to scroll sideways to read.
    /// </summary>
    private const int MaximumSponsors = 16;

    /// <summary>
    /// Enough of an event's own photos for a real gallery — a weekend
    /// tournament's worth of match and podium pictures — without one
    /// competition's settings row growing without bound.
    /// </summary>
    private const int MaximumGalleryPhotos = 60;

    /// <summary>
    /// A colour as the portal stores every one of them: a full six-digit hex
    /// with the hash, nothing shorter. Shared by the accent colour and each
    /// of the theme's four colours so they cannot drift apart.
    /// </summary>
    private const string HexColour = "^#[0-9a-fA-F]{6}$";
}
