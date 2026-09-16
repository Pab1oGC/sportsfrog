using System.Runtime.CompilerServices;

namespace SportFrog.Api.Tests.Features.Competitions.Bulletin;

/// <summary>
/// Declares QuestPDF's license once for the whole test assembly.
/// </summary>
/// <remarks>
/// <c>Program.cs</c> does this for the running API, but a test never runs
/// <c>Program.cs</c> — without this, the very first call to
/// <c>GeneratePdf()</c> in any test throws QuestPDF's own "please configure a
/// license tier" exception rather than the layout error a test is actually
/// trying to catch.
/// </remarks>
internal static class QuestPdfTestLicense
{
    [ModuleInitializer]
    public static void Declare() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
}
