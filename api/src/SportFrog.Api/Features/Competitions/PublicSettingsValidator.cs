using FluentValidation;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Validation;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// What a visitor is shown on a competition's public page: every rule a
/// caller's <see cref="PublicSettings"/> has to satisfy on its own, with no
/// question that needs the rest of the competition to answer.
/// </summary>
/// <remarks>
/// Split out of <see cref="CompetitionContractValidator"/>, which still runs
/// these same rules — now by composing this one with <c>SetValidator</c>
/// inside its own <c>When(contract.Settings?.Public is not null, ...)</c> —
/// rather than by repeating them. <see cref="Features.Competitions.UpdateCompetitionPublicSettings"/>
/// is the other caller: a correction to only the public page should not have
/// to answer for the rest of the competition's contract, and writing the
/// same rule twice under two names is exactly how the two would eventually
/// disagree about what a legal theme is.
/// </remarks>
public sealed class PublicSettingsValidator : AbstractValidator<PublicSettings>
{
    /// <summary>
    /// Enough to sponsor a competition without the strip turning into
    /// something a phone has to scroll sideways to read.
    /// </summary>
    internal const int MaximumSponsors = 16;

    /// <summary>
    /// Enough of an event's own photos for a real gallery — a weekend
    /// tournament's worth of match and podium pictures — without one
    /// competition's settings row growing without bound.
    /// </summary>
    internal const int MaximumGalleryPhotos = 60;

    /// <summary>
    /// A colour as the portal stores every one of them: a full six-digit hex
    /// with the hash, nothing shorter. Shared by the accent colour and each
    /// of the theme's four colours so they cannot drift apart.
    /// </summary>
    internal const string HexColour = "^#[0-9a-fA-F]{6}$";

    public PublicSettingsValidator()
    {
        // A picture field arrives one of two ways: a data URL, freshly
        // picked and waiting to be uploaded, or a key this store already
        // handed out, sent back unchanged because nothing about it changed.
        // IsAcceptable alone would refuse the second shape — it does not
        // look like an image, it looks like the key of one.
        RuleFor(settings => settings.BannerKey)
            .Must(value => PortalPicture.IsStoredKey(value!) || InlinePhoto.IsAcceptable(value))
            .When(settings => settings.BannerKey is not null)
            .WithMessage(InlinePhoto.Requirement);

        RuleFor(settings => settings.LogoKey)
            .Must(value => PortalPicture.IsStoredKey(value!) || InlinePhoto.IsAcceptable(value))
            .When(settings => settings.LogoKey is not null)
            .WithMessage(InlinePhoto.Requirement);

        RuleFor(settings => settings.AccentColor)
            .Matches(HexColour)
            .When(settings => !string.IsNullOrEmpty(settings.AccentColor))
            .WithMessage("El color se escribe como #rrggbb.");

        When(settings => settings.Theme is not null, () =>
        {
            RuleFor(settings => settings.Theme!).SetValidator(new PortalThemeValidator());
        });

        RuleFor(settings => settings.Description)
            .MaximumLength(500)
            .WithMessage("La presentación tiene como máximo 500 caracteres.");

        RuleFor(settings => settings.Instagram).MaximumLength(200);
        RuleFor(settings => settings.Facebook).MaximumLength(200);
        RuleFor(settings => settings.WhatsApp).MaximumLength(200);
        RuleFor(settings => settings.Website).MaximumLength(200);

        RuleFor(settings => settings.Sponsors)
            .Must(sponsors => sponsors == null || sponsors.Count <= MaximumSponsors)
            .WithMessage($"Como máximo {MaximumSponsors} auspiciantes.");

        // Guarded rather than folded into the Must above with ?? []: an
        // empty fallback is a fresh collection expression, and RuleForEach
        // cannot infer which property it is validating from one — it needs
        // the real property access underneath, which only shows up once
        // null is ruled out here instead of inside the expression.
        When(settings => settings.Sponsors is { Count: > 0 }, () =>
        {
            RuleForEach(settings => settings.Sponsors!)
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

        RuleFor(settings => settings.Gallery)
            .Must(gallery => gallery == null || gallery.Count <= MaximumGalleryPhotos)
            .WithMessage($"Como máximo {MaximumGalleryPhotos} fotos.");

        // Mismo motivo que el guard de Sponsors de arriba: RuleForEach
        // necesita la propiedad real, no un ?? [] de por medio.
        When(settings => settings.Gallery is { Count: > 0 }, () =>
        {
            RuleForEach(settings => settings.Gallery!)
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

        // El orden y los nombres de las secciones. Cada fila que el estudio
        // manda ya nombra una clave real -- las cuatro que PortalSection.Keys
        // conoce -- así que lo único que puede fallar es un cliente
        // desactualizado nombrando una que ya no existe, o una futura que
        // todavía no.
        RuleFor(settings => settings.SectionOrder)
            .Must(sections => sections == null || sections.Count <= PortalSection.Keys.Count)
            .WithMessage($"Como máximo {PortalSection.Keys.Count} secciones.");

        When(settings => settings.SectionOrder is { Count: > 0 }, () =>
        {
            RuleForEach(settings => settings.SectionOrder!)
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
    }
}

/// <summary>
/// The portal theme on its own: every field optional — a theme that only
/// named a font is valid — so each rule only runs when that one field was
/// given. The four colours share <see cref="PublicSettingsValidator.HexColour"/>;
/// the eleven choice fields are matched against the same allow-lists the
/// public page defaults from, so a stale client sending a value that was
/// later renamed is told the options rather than silently ignored.
/// </summary>
public sealed class PortalThemeValidator : AbstractValidator<PortalTheme>
{
    public PortalThemeValidator()
    {
        RuleFor(theme => theme.Primary)
            .Matches(PublicSettingsValidator.HexColour)
            .When(theme => !string.IsNullOrEmpty(theme.Primary))
            .WithMessage("El color principal se escribe como #rrggbb.");

        RuleFor(theme => theme.PrimaryContrast)
            .Matches(PublicSettingsValidator.HexColour)
            .When(theme => !string.IsNullOrEmpty(theme.PrimaryContrast))
            .WithMessage("El color del texto sobre el principal se escribe como #rrggbb.");

        RuleFor(theme => theme.Secondary)
            .Matches(PublicSettingsValidator.HexColour)
            .When(theme => !string.IsNullOrEmpty(theme.Secondary))
            .WithMessage("El color secundario se escribe como #rrggbb.");

        RuleFor(theme => theme.Surface)
            .Matches(PublicSettingsValidator.HexColour)
            .When(theme => !string.IsNullOrEmpty(theme.Surface))
            .WithMessage("El color de fondo se escribe como #rrggbb.");

        RuleFor(theme => theme.HeadingFont)
            .Must(value => PortalTheme.Fonts.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.HeadingFont))
            .WithMessage($"Tipografía desconocida. Disponibles: {string.Join(", ", PortalTheme.Fonts.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.Corners)
            .Must(value => PortalTheme.CornerStyles.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.Corners))
            .WithMessage($"Estilo de bordes desconocido. Disponibles: {string.Join(", ", PortalTheme.CornerStyles.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.HeroStyle)
            .Must(value => PortalTheme.HeroStyles.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.HeroStyle))
            .WithMessage($"Estilo de portada desconocido. Disponibles: {string.Join(", ", PortalTheme.HeroStyles.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.HeroGradientTo)
            .Matches(PublicSettingsValidator.HexColour)
            .When(theme => !string.IsNullOrEmpty(theme.HeroGradientTo))
            .WithMessage("El segundo color del degradado se escribe como #rrggbb.");

        RuleFor(theme => theme.Density)
            .Must(value => PortalTheme.Densities.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.Density))
            .WithMessage($"Densidad desconocida. Disponibles: {string.Join(", ", PortalTheme.Densities.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.Decoration)
            .Must(value => PortalTheme.Decorations.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.Decoration))
            .WithMessage($"Decoración desconocida. Disponibles: {string.Join(", ", PortalTheme.Decorations.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.ContentFigure)
            .Must(value => PortalTheme.ContentFigures.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.ContentFigure))
            .WithMessage($"Figura de fondo desconocida. Disponibles: {string.Join(", ", PortalTheme.ContentFigures.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.ContentFigureColor)
            .Matches(PublicSettingsValidator.HexColour)
            .When(theme => !string.IsNullOrEmpty(theme.ContentFigureColor))
            .WithMessage("El color de la figura se escribe como #rrggbb.");

        RuleFor(theme => theme.HeroLayout)
            .Must(value => PortalTheme.HeroLayouts.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.HeroLayout))
            .WithMessage($"Layout de portada desconocido. Disponibles: {string.Join(", ", PortalTheme.HeroLayouts.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.HeroVariant)
            .Must(value => PortalTheme.HeroVariants.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.HeroVariant))
            .WithMessage($"Variante de portada desconocida. Disponibles: {string.Join(", ", PortalTheme.HeroVariants.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.StandingsVariant)
            .Must(value => PortalTheme.StandingsVariants.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.StandingsVariant))
            .WithMessage($"Variante de tabla desconocida. Disponibles: {string.Join(", ", PortalTheme.StandingsVariants.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.MatchCardVariant)
            .Must(value => PortalTheme.MatchCardVariants.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.MatchCardVariant))
            .WithMessage($"Variante de tarjeta de partido desconocida. Disponibles: {string.Join(", ", PortalTheme.MatchCardVariants.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.BracketVariant)
            .Must(value => PortalTheme.BracketVariants.Contains(value!))
            .When(theme => !string.IsNullOrEmpty(theme.BracketVariant))
            .WithMessage($"Variante de llave desconocida. Disponibles: {string.Join(", ", PortalTheme.BracketVariants.Order(StringComparer.Ordinal))}.");

        RuleFor(theme => theme.FocusX)
            .InclusiveBetween(0, 100)
            .When(theme => theme.FocusX is not null)
            .WithMessage("El punto focal horizontal va de 0 a 100.");

        RuleFor(theme => theme.FocusY)
            .InclusiveBetween(0, 100)
            .When(theme => theme.FocusY is not null)
            .WithMessage("El punto focal vertical va de 0 a 100.");
    }
}
