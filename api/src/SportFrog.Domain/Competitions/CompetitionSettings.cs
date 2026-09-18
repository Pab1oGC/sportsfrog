namespace SportFrog.Domain.Competitions;

/// <summary>
/// Settings of a competition that do not deserve a column each.
/// </summary>
/// <remarks>
/// The schema documents two sections and both are here. <see cref="Public"/>
/// decides what a visitor is shown; <see cref="Schedule"/> says when and
/// where the competition can be played, which is what turns a draw into a
/// calendar.
/// </remarks>
public sealed record CompetitionSettings
{
    public PublicSettings? Public { get; init; }

    public ScheduleSettings? Schedule { get; init; }

    public BulletinSettings? Bulletin { get; init; }
}

/// <summary>
/// Where this competition can put a fixture.
/// </summary>
/// <remarks>
/// Which pitches, not when they are free: reserving one is a real-world
/// arrangement an organizer settles with whoever owns it — a phone call, not
/// a fact this software could know on its own — so this stops at naming
/// them. <see cref="ScheduleCalendar.Request"/>'s own date and start time say
/// when a given run should begin; the placement works out what fits from
/// there.
/// </remarks>
public sealed record ScheduleSettings
{
    /// <summary>
    /// Minutes left on a pitch after one match before the next may start.
    /// </summary>
    /// <remarks>
    /// Organization logistics — cleaning, the next team warming up — never a
    /// rule of the sport, which is why it lives here and not on the
    /// reglamento: how long the match itself takes is
    /// <see cref="Rules.MatchDuration"/>'s question, computed or declared
    /// from the jornada's own reglamento, never this competition's. Null
    /// uses a sensible default rather than zero, so a competition that never
    /// touches this still gets some breathing room between matches instead
    /// of none.
    /// </remarks>
    public short? BufferMinutes { get; init; }

    /// <summary>
    /// Which of the organization's pitches this competition may use.
    /// </summary>
    /// <remarks>
    /// Just which ones — nothing about when. A pitch this names is either
    /// free at the moment the placement tries it or it already carries a
    /// booking, and either way that is a fact the database already has;
    /// there is no separate notion of "open hours" left to configure.
    /// </remarks>
    public IReadOnlyList<Guid>? SpaceIds { get; init; }
}

/// <summary>
/// What a visitor sees on the public page, once the competition is published
/// at all.
/// </summary>
/// <remarks>
/// Separate from the competition's own <c>is_public</c>, which is the switch:
/// that decides whether there is a public page, these decide what is on it.
/// The two are not redundant — a competition can be public while its rosters
/// are not, and that combination is the common one where minors play (RNF-16).
///
/// Standings, leaders and classification default to shown, because a
/// published competition with none of them has nothing to publish. Rosters
/// default to hidden: naming the children on a team is a decision someone
/// has to make on purpose.
///
/// <see cref="ShowStandings"/> is the organizer's own switch, but it is not
/// the last word: the public read (<c>ReadPublicCompetition</c>) refuses
/// the section outright for a <see cref="CompetitionFormat.Knockout"/>
/// competition regardless of it, because single elimination has no group
/// stage for a table to describe — the switch would otherwise turn on a
/// page with nothing honest to show.
/// </remarks>
public sealed record PublicSettings
{
    public bool ShowStandings { get; init; } = true;

    public bool ShowLeaders { get; init; } = true;

    /// <summary>
    /// Whether a judged category's classification stage — poomsae's ranking
    /// by score, before its knockout is drawn — appears on the public page.
    /// </summary>
    /// <remarks>
    /// Its own switch rather than folded into <see cref="ShowStandings"/>:
    /// the two never coexist on the same category (one sport is scored by
    /// table, the other by judges), but a competition can run both kinds of
    /// category at once, and an organizer publishing one division's table
    /// should not be assumed to also want another division's judges' scores
    /// public before they are final.
    /// </remarks>
    public bool ShowClassification { get; init; } = true;

    public bool ShowRosters { get; init; }

    /// <summary>
    /// Whether an individual-sport competitor's own photo may appear on the
    /// public bracket, next to their name and club.
    /// </summary>
    /// <remarks>
    /// Off by default, unlike almost everything else in this record. An
    /// athlete's photograph is not the same class of fact as their name: it
    /// stays private everywhere else in this system (see
    /// <c>ReadPublicRoster</c>'s own remarks on RNF-16), and taekwondo — the
    /// one individual sport in the catalog today — runs youth categories.
    /// Publishing a minor's photo on an unauthenticated page is a decision
    /// this switch hands to the organization that actually holds consent for
    /// it, never a default this platform reaches for on its own. The public
    /// read (<c>ReadPublicCalendar</c>) also refuses a photo outright for a
    /// team of more than one athlete — poomsae runs pairs and trios under the
    /// same "is individual" flag as a lone kyorugi fighter, and "the
    /// competitor's photo" is not one well-defined thing there — so this
    /// switch alone is not the last word either, the same shape of rule as
    /// <see cref="ShowStandings"/> above.
    /// </remarks>
    public bool ShowAthletePhotos { get; init; }

    /// <summary>
    /// Whether the event's own photo gallery — <see cref="Gallery"/> —
    /// appears on the public page.
    /// </summary>
    /// <remarks>
    /// Defaults shown, like every other section here: an organizer who went
    /// to the trouble of uploading photos almost always wants them seen.
    /// Whether the tab actually appears is still this switch and
    /// <see cref="Gallery"/> being non-empty together — a gallery with
    /// nothing in it has nothing to publish, the same reasoning
    /// <see cref="Sponsors"/> and its own empty strip already follow.
    /// </remarks>
    public bool ShowGallery { get; init; } = true;

    /// <summary>
    /// The cover image behind the competition's name, as a storage key —
    /// never a URL. Absent means the plain brand background every
    /// competition had before this existed.
    /// </summary>
    public string? BannerKey { get; init; }

    /// <summary>
    /// The competition's own mark, as a storage key — never a URL. A
    /// different picture from <see cref="BannerKey"/> and not a crop of it:
    /// the banner is a wide cover meant to sit behind the competition's name,
    /// and a badge-shaped mark squeezed into that space reads as a mistake.
    /// This is the shape a logo actually needs — square-ish, standing on its
    /// own — for wherever the competition is represented by a small mark
    /// rather than a cover: alongside its name on a listing, or printed on a
    /// document.
    /// </summary>
    public string? LogoKey { get; init; }

    /// <summary>
    /// The competition's own color for its public page, as "#rrggbb".
    /// </summary>
    /// <remarks>
    /// Predates <see cref="Theme"/> and kept for the competitions that only
    /// ever set this. It is the fallback the theme's primary colour is read
    /// from when <see cref="Theme"/> is absent, so a page that set nothing
    /// but this keeps exactly the look it had — see
    /// <see cref="PortalTheme.Resolve"/>. New customization writes
    /// <see cref="Theme"/>; nothing writes both.
    /// </remarks>
    public string? AccentColor { get; init; }

    /// <summary>
    /// The full visual system the public page is dressed in: colours beyond a
    /// single accent, the display font its headings are set in, how square its
    /// corners are, and how the cover renders.
    /// </summary>
    /// <remarks>
    /// Absent means the plain SportFrog theme every competition had before
    /// this existed — the same page <see cref="AccentColor"/> alone still
    /// produces. Every field inside is itself optional: an unset one is filled
    /// from <see cref="PortalTheme.Resolve"/>'s defaults, which are the base
    /// theme's own values, so a half-filled theme is never a broken page.
    /// </remarks>
    public PortalTheme? Theme { get; init; }

    /// <summary>A line or two under the name: who runs this, what it is for.</summary>
    public string? Description { get; init; }

    public string? Instagram { get; init; }

    public string? Facebook { get; init; }

    public string? WhatsApp { get; init; }

    public string? Website { get; init; }

    /// <summary>Who paid to appear on the page, in the order they appear.</summary>
    public IReadOnlyList<SponsorLink>? Sponsors { get; init; }

    /// <summary>Photos from the event itself, in the order they appear.</summary>
    public IReadOnlyList<GalleryPhoto>? Gallery { get; init; }

    /// <summary>
    /// The order the page's own sections appear in, and whatever name each
    /// one was given instead of its default — everything but whether one is
    /// shown at all, which is still <see cref="ShowStandings"/> and its
    /// siblings above. Absent means the order this page has always had.
    /// </summary>
    /// <remarks>
    /// Kept apart from the show/hide switches on purpose: this list only
    /// answers "in what order, called what", never "shown or not" — folding
    /// visibility in here too would give a section two different ways to be
    /// hidden that would eventually disagree. See
    /// <see cref="PortalSection.Resolve"/> for how a sparse or absent list
    /// becomes the four sections the page actually has, in some order, each
    /// with a name.
    /// </remarks>
    public IReadOnlyList<PortalSection>? SectionOrder { get; init; }
}

/// <summary>
/// The organizer's own words for the competition's bulletin — the document
/// every category, its rules and its scoring are announced in ahead of the
/// draw, before there is any team or fixture to speak of yet.
/// </summary>
/// <remarks>
/// Deliberately narrow: what the schema already knows — categories, the
/// scoring each one plays under, tiebreakers — is composed straight from the
/// competition's own categories and rulesets when the bulletin is rendered
/// (see <c>CompetitionBulletinData</c>), not duplicated here where it could
/// drift out of sync with the categories actually entered. This holds only
/// what nothing else in the system says: the organizer's own prose.
/// </remarks>
public sealed record BulletinSettings
{
    /// <summary>Opens the document — what the competition is, who runs it, why it exists.</summary>
    public string? Introduction { get; init; }

    /// <summary>
    /// Disciplinary rules: what earns a card, a suspension, a disqualification.
    /// Nothing in the schema models this — a ruleset prices a match's outcome,
    /// not a person's conduct — so it is the organizer's prose or it is
    /// nothing at all.
    /// </summary>
    public string? Sanctions { get; init; }

    /// <summary>
    /// Whatever else the bulletin needs to say that has no column of its
    /// own: venues, registration deadlines, protests, anything specific to
    /// this one competition.
    /// </summary>
    public string? GeneralProvisions { get; init; }

    /// <summary>Who to write to or call with a question, and how.</summary>
    public string? ContactInfo { get; init; }
}

/// <summary>One name in the strip of sponsors a public page may show.</summary>
public sealed record SponsorLink
{
    /// <summary>The mark itself, as a storage key.</summary>
    public required string LogoKey { get; init; }

    public string? Name { get; init; }

    /// <summary>Where the mark links to, if it should be clickable at all.</summary>
    public string? Url { get; init; }
}

/// <summary>One photo in the event's own gallery.</summary>
public sealed record GalleryPhoto
{
    /// <summary>The photo itself, as a storage key.</summary>
    public required string Key { get; init; }

    /// <summary>A line under the photo — who, where, what moment. Never required: a gallery is still a gallery unlabelled.</summary>
    public string? Caption { get; init; }
}

/// <summary>
/// The visual system a competition's public page is dressed in, as it is
/// stored: every field optional, because the organizer fills in as much or as
/// little as they want and the rest is defaulted when the page is built.
/// </summary>
/// <remarks>
/// This is the raw shape — the editor reads and writes exactly this. What the
/// public page actually renders with is <see cref="ResolvedPortalTheme"/>,
/// produced by <see cref="Resolve"/>, which is where the defaults and the
/// <see cref="PublicSettings.AccentColor"/> fallback live. Keeping the two
/// apart means the stored shape can stay sparse (a theme that only picked a
/// font is three words of JSON) without every reader re-deriving the same
/// fallbacks.
///
/// The closed-set fields (<see cref="HeadingFont"/>, <see cref="Corners"/>,
/// <see cref="HeroStyle"/>, <see cref="Density"/>,
/// <see cref="Decoration"/>, <see cref="HeroLayout"/>, <see cref="HeroVariant"/>,
/// <see cref="StandingsVariant"/>, <see cref="MatchCardVariant"/>,
/// <see cref="BracketVariant"/>) are plain strings
/// checked against an allow-list by the contract validator, the same way
/// <see cref="CompetitionFormat"/> and the capture level are — an unknown
/// value is answered with the list of accepted ones, not a bare 400.
/// </remarks>
public sealed record PortalTheme
{
    /// <summary>
    /// The brand colour, as "#rrggbb": the cover background, the tab
    /// underline, primary buttons, the selected category chip.
    /// </summary>
    public string? Primary { get; init; }

    /// <summary>
    /// The colour text and icons sitting on <see cref="Primary"/> are drawn
    /// in, as "#rrggbb". Absent means "work it out" — the page picks black or
    /// white by contrast against <see cref="Primary"/>.
    /// </summary>
    public string? PrimaryContrast { get; init; }

    /// <summary>
    /// A second colour for links and secondary actions, as "#rrggbb". Absent
    /// means it tracks <see cref="Primary"/>.
    /// </summary>
    public string? Secondary { get; init; }

    /// <summary>
    /// The page background behind the content, as "#rrggbb". Absent means the
    /// base theme's own near-white.
    /// </summary>
    public string? Surface { get; init; }

    /// <summary>
    /// Which display face the headings are set in — one of
    /// <see cref="Fonts"/>. Absent, or <c>inter</c>, means the body font, so
    /// nothing extra is loaded.
    /// </summary>
    public string? HeadingFont { get; init; }

    /// <summary>How square the corners are — one of <see cref="CornerStyles"/>.</summary>
    public string? Corners { get; init; }

    /// <summary>How the cover renders — one of <see cref="HeroStyles"/>.</summary>
    public string? HeroStyle { get; init; }

    /// <summary>
    /// The second colour of the cover's fade, as "#rrggbb", when
    /// <see cref="HeroStyle"/> is <c>gradient</c>. Absent means the fade is
    /// worked out automatically — <see cref="Primary"/> darkened — the same
    /// gradient this field didn't exist to override before.
    /// </summary>
    public string? HeroGradientTo { get; init; }

    /// <summary>
    /// Whether the competition's own logo, in the cover, sits on a solid
    /// backdrop. Defaults to <c>true</c> — the look every competition had
    /// before this field existed. Turned off for a mark that already reads
    /// fine on its own (already opaque, or designed to sit directly on a
    /// colour), where a second frame behind it would be a frame around a
    /// frame.
    /// </summary>
    /// <remarks>
    /// Only the competition's logo — never <see cref="Primary"/>-derived
    /// colour, never the organization's own logo in the navigation bar, and
    /// never a closed set like <see cref="HeroStyle"/>: this one is either
    /// on or it is not.
    /// </remarks>
    public bool ShowLogoBackground { get; init; } = true;

    /// <summary>
    /// Where the banner keeps its subject when the cover is cropped narrower
    /// than the picture — 0 to 100, left to right. Absent means centred.
    /// </summary>
    /// <remarks>
    /// Only matters for <see cref="HeroStyle"/> <c>image</c>, and only when
    /// the crop actually loses something: a phone's narrow strip of a wide
    /// team photo needs to keep the faces, not the sponsor board's empty
    /// half. <see cref="FocusY"/> is its vertical twin; the two are set and
    /// read together but validated and defaulted independently; because
    /// which axis a given banner needs adjusted is never the same twice.
    /// </remarks>
    public double? FocusX { get; init; }

    /// <summary>Where the banner keeps its subject, top to bottom — 0 to 100. See <see cref="FocusX"/>.</summary>
    public double? FocusY { get; init; }

    /// <summary>
    /// How much air the whole page gets — section spacing, card padding,
    /// the type scale — one of <see cref="Densities"/>.
    /// </summary>
    /// <remarks>
    /// Applied as a single multiplier on the page's spacing unit, not as a
    /// per-component setting: every section of the public page reads its own
    /// padding and gaps off that one number, so this one field reaches the
    /// whole page instead of needing a density knob on each section.
    /// </remarks>
    public string? Density { get; init; }

    /// <summary>
    /// The decorative texture behind the cover's text — one of
    /// <see cref="Decorations"/>. Absent or <c>none</c> draws nothing.
    /// </summary>
    /// <remarks>
    /// One motif (diagonal lines), two intensities — not a library of
    /// patterns to choose from. It sits behind the cover's text and never
    /// changes it, the same way a stadium scoreboard's texture never
    /// competes with the score printed on it.
    /// </remarks>
    public string? Decoration { get; init; }

    /// <summary>
    /// The decorative shape tiled behind the page's content — everything
    /// below the cover — as one of <see cref="ContentFigures"/>. Absent or
    /// <c>none</c> draws nothing.
    /// </summary>
    /// <remarks>
    /// Never the cover: <see cref="Decoration"/> already owns that surface,
    /// and the two are independent so a competition can have one without
    /// the other. Rendered as a single-colour silhouette of whichever piece
    /// of artwork was chosen — <see cref="ContentFigureColor"/> is that
    /// colour, not a second choice of shape.
    /// </remarks>
    public string? ContentFigure { get; init; }

    /// <summary>
    /// The colour <see cref="ContentFigure"/> is tinted, as "#rrggbb".
    /// Absent means it tracks <see cref="Primary"/> — same fallback as
    /// <see cref="Secondary"/>, for the same reason: most competitions that
    /// turn this on want it in their own colour, not a second one to pick.
    /// </summary>
    public string? ContentFigureColor { get; init; }

    /// <summary>How the cover's text is arranged — one of <see cref="HeroLayouts"/>.</summary>
    /// <remarks>
    /// A second arrangement of the same pieces the cover already draws
    /// (name, status, countdown, champion) — not a second cover design.
    /// Independent from whether the cover is shown dense (the studio's
    /// narrow preview) or full width (the public page): a competition that
    /// chose <c>centered</c> sees it centered in both places.
    /// </remarks>
    public string? HeroLayout { get; init; }

    /// <summary>
    /// Which cover the page is actually built from — one of
    /// <see cref="HeroVariants"/>. <c>standard</c> is the two-piece cover
    /// every other field here still describes (colours, <see cref="HeroLayout"/>,
    /// <see cref="Decoration"/>...); the other three are a different
    /// composition of the same underlying facts, not a restyling of the
    /// same one.
    /// </summary>
    /// <remarks>
    /// <see cref="HeroLayout"/> only matters for <c>standard</c> — the other
    /// three fix their own arrangement and ignore it, the same way
    /// <see cref="FocusX"/> only matters for <c>image</c>. <c>live</c> shows
    /// the competition's own current match when there is exactly one in
    /// progress; with zero or more than one, it falls back to
    /// <c>standard</c> rather than guess which to show — the public read
    /// resolves that fact, this field only says the organiser asked for it
    /// when available.
    /// </remarks>
    public string? HeroVariant { get; init; }

    /// <summary>
    /// Which layout the standings table renders in — one of
    /// <see cref="StandingsVariants"/>. <c>standard</c> is today's
    /// <c>DataGrid</c>; the other two are a different arrangement of the
    /// same rows, not a restyling of the same table.
    /// </summary>
    /// <remarks>
    /// The rule that a public table is never reorderable by a visitor — the
    /// ranking is the ruleset's answer, not a spreadsheet — holds for every
    /// variant by construction: <c>cards</c> and <c>editorial</c> have no
    /// column headers to click in the first place.
    /// </remarks>
    public string? StandingsVariant { get; init; }

    /// <summary>
    /// Which layout a match's card renders in, in the calendar — one of
    /// <see cref="MatchCardVariants"/>. Never reaches the knockout bracket
    /// (<c>Llave</c>/<c>CruceLlave</c> on the public page), which has its
    /// own independent <see cref="BracketVariant"/>.
    /// </summary>
    public string? MatchCardVariant { get; init; }

    /// <summary>
    /// Which layout a knockout tie renders in, inside the bracket — one of
    /// <see cref="BracketVariants"/>. Independent from
    /// <see cref="MatchCardVariant"/>: the bracket's own champion banner
    /// and column scroll never change, only the tie card inside each
    /// column.
    /// </summary>
    public string? BracketVariant { get; init; }

    /// <summary>The display faces a heading may be set in. <c>inter</c> is the body font — no extra load.</summary>
    public static readonly IReadOnlySet<string> Fonts = new HashSet<string>(StringComparer.Ordinal)
    {
        "inter", "oswald", "bebas-neue", "anton", "barlow-condensed", "archivo-black", "teko", "montserrat",
    };

    /// <summary>How square the corners are: <c>sharp</c> 0px, <c>soft</c> the base 12px, <c>round</c> 22px.</summary>
    public static readonly IReadOnlySet<string> CornerStyles = new HashSet<string>(StringComparer.Ordinal)
    {
        "sharp", "soft", "round",
    };

    /// <summary>
    /// How the cover renders: <c>solid</c> a flat fill, <c>gradient</c> a fade
    /// from the primary, <c>image</c> the banner (falling back to
    /// <c>solid</c> when no banner is set).
    /// </summary>
    public static readonly IReadOnlySet<string> HeroStyles = new HashSet<string>(StringComparer.Ordinal)
    {
        "solid", "gradient", "image",
    };

    /// <summary><c>compact</c> tightens the page's spacing unit, <c>spacious</c> loosens it. <c>normal</c> is the base theme's own value.</summary>
    public static readonly IReadOnlySet<string> Densities = new HashSet<string>(StringComparer.Ordinal)
    {
        "compact", "normal", "spacious",
    };

    /// <summary>How strong the cover's decorative texture reads. <c>none</c> draws nothing.</summary>
    public static readonly IReadOnlySet<string> Decorations = new HashSet<string>(StringComparer.Ordinal)
    {
        "none", "subtle", "bold",
    };

    /// <summary>
    /// Which shape tiles behind the page's content — see
    /// <see cref="ContentFigure"/>. <c>none</c> draws nothing; the rest name
    /// one piece of artwork each, not a style applied to all of them.
    /// </summary>
    public static readonly IReadOnlySet<string> ContentFigures = new HashSet<string>(StringComparer.Ordinal)
    {
        "none", "wave", "curve-line", "shiny-overlay", "colored-patterns", "contour-line",
    };

    /// <summary><c>standard</c> is the two-column cover; <c>centered</c> stacks everything in the middle.</summary>
    public static readonly IReadOnlySet<string> HeroLayouts = new HashSet<string>(StringComparer.Ordinal)
    {
        "standard", "centered",
    };

    /// <summary>Which cover composition the page renders — see <see cref="HeroVariant"/>.</summary>
    public static readonly IReadOnlySet<string> HeroVariants = new HashSet<string>(StringComparer.Ordinal)
    {
        "standard", "scoreboard", "editorial", "live",
    };

    /// <summary>Which standings layout the page renders — see <see cref="StandingsVariant"/>.</summary>
    public static readonly IReadOnlySet<string> StandingsVariants = new HashSet<string>(StringComparer.Ordinal)
    {
        "standard", "cards", "editorial",
    };

    /// <summary>Which match card layout the calendar renders — see <see cref="MatchCardVariant"/>.</summary>
    public static readonly IReadOnlySet<string> MatchCardVariants = new HashSet<string>(StringComparer.Ordinal)
    {
        "standard", "compact", "matchup",
    };

    /// <summary>Which tie card layout the bracket renders — see <see cref="BracketVariant"/>.</summary>
    public static readonly IReadOnlySet<string> BracketVariants = new HashSet<string>(StringComparer.Ordinal)
    {
        "standard", "compact", "detailed",
    };

    /// <summary>The primary colour a page falls back to when nothing set one at all.</summary>
    private const string BrandGreen = "#1B8A2E";

    /// <summary>
    /// The theme the public page is actually built from, or <c>null</c> when
    /// the competition never dressed its page up — in which case the page
    /// stays on the plain SportFrog theme, unchanged.
    /// </summary>
    /// <remarks>
    /// The fallback chain for the primary colour is
    /// <see cref="Primary"/> → <see cref="PublicSettings.AccentColor"/> →
    /// <see cref="BrandGreen"/>, so a competition that only ever set the old
    /// accent colour resolves to a theme whose primary is that colour and
    /// whose every other field is a base-theme default: the same page it had,
    /// now applied consistently instead of only to the cover strip.
    /// </remarks>
    public static ResolvedPortalTheme? Resolve(PublicSettings? settings)
    {
        var theme = settings?.Theme;
        var legacyAccent = string.IsNullOrWhiteSpace(settings?.AccentColor) ? null : settings!.AccentColor;

        if (theme is null && legacyAccent is null)
        {
            return null;
        }

        var primary = FirstNonBlank(theme?.Primary, legacyAccent) ?? BrandGreen;

        // A competition that set a banner before the hero style existed still
        // expects that banner shown: its unset hero style defaults to
        // "image", not "solid", so the page it had does not lose its cover.
        var defaultHero = string.IsNullOrWhiteSpace(settings?.BannerKey) ? "solid" : "image";

        return new ResolvedPortalTheme(
            Primary: primary,
            PrimaryContrast: NullIfBlank(theme?.PrimaryContrast),
            Secondary: FirstNonBlank(theme?.Secondary) ?? primary,
            Surface: NullIfBlank(theme?.Surface),
            HeadingFont: OneOf(theme?.HeadingFont, Fonts, "inter"),
            Corners: OneOf(theme?.Corners, CornerStyles, "soft"),
            HeroStyle: OneOf(theme?.HeroStyle, HeroStyles, defaultHero),
            HeroGradientTo: NullIfBlank(theme?.HeroGradientTo),
            ShowLogoBackground: theme?.ShowLogoBackground ?? true,
            FocusX: InRange(theme?.FocusX, 50),
            FocusY: InRange(theme?.FocusY, 50),
            Density: OneOf(theme?.Density, Densities, "normal"),
            Decoration: OneOf(theme?.Decoration, Decorations, "none"),
            ContentFigure: OneOf(theme?.ContentFigure, ContentFigures, "none"),
            ContentFigureColor: NullIfBlank(theme?.ContentFigureColor),
            HeroLayout: OneOf(theme?.HeroLayout, HeroLayouts, "standard"),
            HeroVariant: OneOf(theme?.HeroVariant, HeroVariants, "standard"),
            StandingsVariant: OneOf(theme?.StandingsVariant, StandingsVariants, "standard"),
            MatchCardVariant: OneOf(theme?.MatchCardVariant, MatchCardVariants, "standard"),
            BracketVariant: OneOf(theme?.BracketVariant, BracketVariants, "standard"));
    }

    /// <summary>A 0-100 axis, or the default when absent or somehow out of range.</summary>
    private static double InRange(double? value, double fallback) =>
        value is >= 0 and <= 100 ? value.Value : fallback;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string OneOf(string? value, IReadOnlySet<string> allowed, string fallback)
    {
        var normalized = value?.Trim();
        return normalized is not null && allowed.Contains(normalized) ? normalized : fallback;
    }
}

/// <summary>
/// A <see cref="PortalTheme"/> with every choice made: the shape the public
/// page renders from, handed straight out by the public competition read.
/// Only the genuinely optional colours stay nullable — the page derives
/// <see cref="PrimaryContrast"/> from contrast, lets <see cref="Surface"/>
/// fall through to the base theme, and works <see cref="HeroGradientTo"/>
/// and <see cref="ContentFigureColor"/> out from <see cref="Primary"/> — all
/// four are the browser's job, not this record's.
/// </summary>
public sealed record ResolvedPortalTheme(
    string Primary,
    string? PrimaryContrast,
    string Secondary,
    string? Surface,
    string HeadingFont,
    string Corners,
    string HeroStyle,
    string? HeroGradientTo,
    bool ShowLogoBackground,
    double FocusX,
    double FocusY,
    string Density,
    string Decoration,
    string ContentFigure,
    string? ContentFigureColor,
    string HeroLayout,
    string HeroVariant,
    string StandingsVariant,
    string MatchCardVariant,
    string BracketVariant);

/// <summary>
/// One of the public page's own sections, as it is stored: which one
/// (<see cref="Keys"/>), and the name it was given instead of its default,
/// if any.
/// </summary>
/// <remarks>
/// The stored list is deliberately allowed to be short, out of order, or
/// missing entirely — an organizer who dragged one section to the top
/// touched nothing about the other three, so nothing about them should have
/// to be written down again. <see cref="Resolve"/> is where a list like
/// that becomes the four sections the page actually renders, each named and
/// in a definite order.
/// </remarks>
public sealed record PortalSection
{
    /// <summary>One of <see cref="Keys"/>.</summary>
    public required string Key { get; init; }

    /// <summary>What this section is called instead of its default name, if anything.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// Standings, leaders, a judged category's classification, the
    /// calendar, and the event's own photo gallery — the whole set.
    /// </summary>
    /// <remarks>
    /// The calendar and the bracket are in this list because their position
    /// is still an organizer's choice — a competition that is really about
    /// the fixture can lead with it — but not because either can be turned
    /// off: nothing in <see cref="PublicSettings"/> can hide the calendar,
    /// and the bracket has no switch of its own either — it shows up only
    /// once the public read finds at least one match with a knockout phase,
    /// the same way <c>classification</c> only really applies to a judged
    /// category. <see cref="Resolve"/> does not try to guess a reason either
    /// one should be hidden beyond that.
    ///
    /// "gallery" and "bracket" were each added after the ones before them —
    /// new keys append rather than replace, which is what lets a
    /// competition's list saved before either existed still resolve to the
    /// full set instead of leaving the new one out.
    /// </remarks>
    public static readonly IReadOnlySet<string> Keys = new HashSet<string>(StringComparer.Ordinal)
    {
        "standings", "leaders", "classification", "calendar", "gallery", "bracket",
    };

    private static readonly IReadOnlyDictionary<string, string> DefaultLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["standings"] = "Tabla de posiciones",
        ["leaders"] = "Líderes",
        ["classification"] = "Clasificación",
        ["calendar"] = "Calendario",
        ["gallery"] = "Fotos",
        ["bracket"] = "Llave",
    };

    /// <summary>The order every competition had before "gallery"/"bracket" existed, which is still where a new one appends.</summary>
    private static readonly string[] DefaultOrder = ["standings", "leaders", "classification", "calendar", "gallery", "bracket"];

    /// <summary>
    /// The sections to render, in order, each with the name it will
    /// actually be shown under.
    /// </summary>
    /// <remarks>
    /// An unknown key is dropped rather than kept as a section nothing knows
    /// how to render, and a repeated key keeps only its first appearance —
    /// both defensive rather than reachable through the studio, which only
    /// ever writes one row per key. Whatever the stored list left out is
    /// appended afterwards in the default order, which is what makes a list
    /// saved before a fifth section is ever added — or one hand-edited down
    /// to a single entry — still resolve to all four instead of silently
    /// losing the rest.
    /// </remarks>
    public static IReadOnlyList<ResolvedPortalSection> Resolve(IReadOnlyList<PortalSection>? sections)
    {
        var resolved = new List<ResolvedPortalSection>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in sections ?? [])
        {
            if (!Keys.Contains(section.Key) || !seen.Add(section.Key))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(section.Label) ? DefaultLabels[section.Key] : section.Label.Trim();
            resolved.Add(new ResolvedPortalSection(section.Key, label));
        }

        foreach (var key in DefaultOrder)
        {
            if (seen.Add(key))
            {
                resolved.Add(new ResolvedPortalSection(key, DefaultLabels[key]));
            }
        }

        return resolved;
    }
}

/// <summary>A <see cref="PortalSection"/> with its name settled, in the order it renders.</summary>
public sealed record ResolvedPortalSection(string Key, string Label);
