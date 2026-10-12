namespace SportFrog.Domain.Documents;

/// <summary>A rectangle in millimetres, relative to one face's own top-left corner.</summary>
public readonly record struct CredentialBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// Where every block of the decreed credential sits, in millimetres.
/// </summary>
/// <remarks>
/// The structure is the one the reference credential of the Iquique 2016
/// Bolivarian Games shows, and it is the same on both faces:
///
/// Front: photograph and category at the top, the name block under them, the
/// services with the visible number beside them, the discipline and venue
/// boxes low on the card, and the zone band along the foot.
///
/// Back: the legal notice in two columns, the identity block with its QR and
/// visible number, the glossary of every code in two columns, the same
/// discipline and venue boxes, and the same zone band.
///
/// The positions are a first, internally consistent layout sized from the
/// face's known measurements (165 × 216 mm) and proportions read off the
/// reference. Nobody has measured the physical card, so a corrected
/// measurement is a changed number here, not a changed drawing method.
/// </remarks>
public static class CredentialLayout
{
    // -------------------------------------------------------------------
    // The sheet: one printed page, back face on the left, front face on
    // the right, folded down the middle to become the two-sided card.
    // -------------------------------------------------------------------

    public const double FaceWidthMm = 165;

    public const double FaceHeightMm = 216;

    public const double SheetWidthMm = FaceWidthMm * 2;

    public const double SheetHeightMm = FaceHeightMm;

    /// <summary>Where the front face starts, on the sheet's own x axis.</summary>
    public const double FrontOffsetMm = FaceWidthMm;

    public const double SideMarginMm = 10;

    /// <summary>The width every block that spans a face fills, between the margins.</summary>
    public const double ContentWidthMm = FaceWidthMm - (SideMarginMm * 2);

    public const double BoxGapMm = 2;

    // -------------------------------------------------------------------
    // Zone band — drawn once per face from the same data, so "identical on
    // both faces" is a property of calling one method twice.
    // -------------------------------------------------------------------

    public const double FooterHeightMm = 26;

    public static readonly CredentialBox FooterBand =
        new(0, FaceHeightMm - FooterHeightMm, FaceWidthMm, FooterHeightMm);

    /// <summary>
    /// The discipline and venue boxes, low on both faces, just above the band.
    /// </summary>
    public const double CodeRowHeightMm = 16;

    public static readonly CredentialBox CodeRow = new(
        SideMarginMm, FooterBand.Y - 8 - CodeRowHeightMm, ContentWidthMm, CodeRowHeightMm);

    /// <summary>The width of one discipline or venue box. They sit left-aligned, as the reference does.</summary>
    public const double CodeBoxWidthMm = 30;

    // -------------------------------------------------------------------
    // Front face
    // -------------------------------------------------------------------

    /// <summary>Portrait, 4:5 — the ratio the specification gives directly.</summary>
    public static readonly CredentialBox FrontPhoto = new(SideMarginMm, SideMarginMm, 48, 60);

    /// <summary>
    /// The competition's logo and, below it, the accreditation category's own
    /// coloured box — to the right of the photo, the same height as it.
    /// </summary>
    public static readonly CredentialBox FrontHeader = new(
        FrontPhoto.Right + 6, SideMarginMm, FaceWidthMm - SideMarginMm - FrontPhoto.Right - 6, 60);

    public static readonly CredentialBox FrontCompetitionLogo =
        FrontHeader with { Height = 32 };

    public static readonly CredentialBox FrontCategoryBox = new(
        FrontHeader.X, FrontCompetitionLogo.Bottom + 4, FrontHeader.Width, FrontHeader.Bottom - FrontCompetitionLogo.Bottom - 4);

    /// <summary>Where the full name and the role and club lines start — under the photo row.</summary>
    public const double NameBlockTopMm = 78;

    public const double NameBlockHeightMm = 36;

    public static readonly CredentialBox NameBlock = new(
        SideMarginMm, NameBlockTopMm, ContentWidthMm, NameBlockHeightMm);

    public const double ClubLogoSizeMm = 11;

    public const double ServicesRowHeightMm = 16;

    public const double ServicesRowTopMm = 122;

    /// <summary>The services row, leaving room on its right for the visible number.</summary>
    public static readonly CredentialBox FrontServicesRow = new(
        SideMarginMm, ServicesRowTopMm, ContentWidthMm - 46 - BoxGapMm * 2, ServicesRowHeightMm);

    /// <summary>The visible number, printed large beside the services it belongs with.</summary>
    public static readonly CredentialBox FrontVisibleId = new(
        FrontServicesRow.Right + 4, ServicesRowTopMm, 46 - 4, ServicesRowHeightMm);

    // -------------------------------------------------------------------
    // Back face
    // -------------------------------------------------------------------

    /// <summary>
    /// The legal notice, split across two columns — a newspaper-style flow,
    /// not two languages: the card is Spanish-only by decision.
    /// </summary>
    public const double LegalTopMm = SideMarginMm;

    public const double LegalHeightMm = 56;

    public const double LegalColumnGapMm = 6;

    public static readonly CredentialBox LegalColumnLeft = new(
        SideMarginMm, LegalTopMm, (ContentWidthMm - LegalColumnGapMm) / 2, LegalHeightMm);

    public static readonly CredentialBox LegalColumnRight =
        LegalColumnLeft with { X = LegalColumnLeft.Right + LegalColumnGapMm };

    /// <summary>The identity block: a small photograph, the name and role, and the QR beside them.</summary>
    public const double BackIdentityTopMm = LegalTopMm + LegalHeightMm + 10;

    public const double BackIdentityHeightMm = 38;

    public static readonly CredentialBox BackMiniPhoto = new(SideMarginMm, BackIdentityTopMm, 30, 38);

    public const double BackQrSizeMm = 30;

    /// <summary>The QR, at the block's right edge. The visible number sits under it.</summary>
    public static readonly CredentialBox BackQr = new(
        FaceWidthMm - SideMarginMm - BackQrSizeMm, BackIdentityTopMm, BackQrSizeMm, BackQrSizeMm);

    public static readonly CredentialBox BackVisibleId = new(
        BackQr.X, BackQr.Bottom + 2, BackQrSizeMm, 7);

    public static readonly CredentialBox BackIdentityText = new(
        BackMiniPhoto.Right + 4, BackIdentityTopMm, BackQr.X - BackMiniPhoto.Right - 4 - 4, BackIdentityHeightMm);

    /// <summary>The glossary of every code the card prints, in two columns, from under the identity block to the code row.</summary>
    public static readonly CredentialBox BackGlossary = new(
        SideMarginMm,
        BackIdentityTopMm + BackIdentityHeightMm + 2,
        ContentWidthMm,
        CodeRow.Y - (BackIdentityTopMm + BackIdentityHeightMm + 2) - 2);

    /// <summary>The height of one glossary entry, and how far apart the entries sit.</summary>
    public const double GlossaryEntryHeightMm = 7;

    public const double GlossaryEntryGapMm = 1;
}
