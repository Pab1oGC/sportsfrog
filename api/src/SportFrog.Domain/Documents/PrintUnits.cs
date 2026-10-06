namespace SportFrog.Domain.Documents;

/// <summary>
/// The unit conversion every PDF renderer in this feature needs: a PDF
/// measures type in points, a layout measures the page in millimetres.
/// </summary>
/// <remarks>
/// One constant rather than one per renderer. A point is 1/72 of an inch
/// regardless of what is being drawn, so <see cref="PointsPerMillimetre"/>
/// is a fact about the PDF format, not something a certificate's renderer
/// and a credential's renderer each get to define their own copy of.
/// </remarks>
public static class PrintUnits
{
    public const float PointsPerMillimetre = 72f / 25.4f;
}
