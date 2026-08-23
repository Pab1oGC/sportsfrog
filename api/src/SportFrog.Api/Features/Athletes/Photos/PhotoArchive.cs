using System.IO.Compression;

namespace SportFrog.Api.Features.Athletes.Photos;

/// <summary>One image found in an uploaded archive.</summary>
/// <param name="Name">The name as it appeared, for the report.</param>
/// <param name="Key">The name reduced to what is matched against a document.</param>
internal sealed record ArchivedPhoto(string Name, string Key, byte[] Content);

/// <summary>What an archive turned out to contain.</summary>
/// <param name="Problem">Something wrong with the archive itself. Null when it was read.</param>
/// <param name="Ignored">Entries that were not images, named so nobody wonders.</param>
internal sealed record ArchiveContents(
    string? Problem,
    IReadOnlyList<ArchivedPhoto> Photos,
    IReadOnlyList<string> Ignored);

/// <summary>
/// Opens an archive of photographs, carefully.
/// </summary>
/// <remarks>
/// A zip file uploaded over the internet is the most hostile input this
/// system accepts, and almost none of the danger is obvious.
///
/// Entry names are attacker-controlled and may say <c>../../etc/passwd</c>.
/// Nothing here writes to disk, but the defence is structural rather than a
/// check that could be forgotten: only the base name is ever read, so a path
/// has nowhere to go.
///
/// Declared sizes are attacker-controlled too. A few kilobytes can declare —
/// and genuinely decompress to — gigabytes, so nothing trusts
/// <see cref="ZipArchiveEntry.Length"/>: every entry is read through a
/// ceiling, and a running total across the whole archive stops the same trick
/// spread over a thousand small entries.
///
/// And the ordinary case is handled too, which matters more day to day:
/// people zip a folder, and macOS puts its own metadata inside. Those are
/// skipped rather than reported as failures, because a report full of
/// __MACOSX entries is a report nobody reads.
/// </remarks>
internal static class PhotoArchive
{
    /// <summary>Past any club's squad and short of an archive of a season.</summary>
    public const int MaximumEntries = 1000;

    /// <summary>Per photograph, before anything is decoded.</summary>
    private const int MaximumEntryBytes = 15 * 1024 * 1024;

    /// <summary>
    /// Across the whole archive, decompressed. This is the ceiling that makes
    /// a compression bomb finite: the archive itself may be a hundred
    /// megabytes, and what it expands to may not be unbounded.
    /// </summary>
    private const long MaximumTotalBytes = 600L * 1024 * 1024;

    private static readonly string[] Images = [".jpg", ".jpeg", ".png", ".webp"];

    public static ArchiveContents Read(Stream file)
    {
        ZipArchive archive;

        try
        {
            archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return new ArchiveContents(
                "No se pudo abrir el archivo como .zip. Tiene que ser un comprimido con las "
                    + "fotos adentro.",
                [], []);
        }

        using (archive)
        {
            if (archive.Entries.Count > MaximumEntries)
            {
                return new ArchiveContents(
                    $"El comprimido tiene más de {MaximumEntries} archivos.", [], []);
            }

            var photos = new List<ArchivedPhoto>();
            var ignored = new List<string>();
            long total = 0;

            foreach (var entry in archive.Entries)
            {
                // Only the base name, always. A folder inside the archive is
                // how people zip things, and a name that tries to climb out
                // of one has nowhere to climb to once the directories are
                // dropped.
                var name = Path.GetFileName(entry.FullName);

                if (name.Length == 0 || entry.FullName.Contains("__MACOSX", StringComparison.Ordinal))
                {
                    // A directory entry, or the metadata folder macOS adds to
                    // every archive it makes. Neither is worth a line in
                    // somebody's report.
                    continue;
                }

                if (name.StartsWith('.') || !Images.Contains(
                        Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                {
                    ignored.Add(name);
                    continue;
                }

                using var contents = entry.Open();

                if (ReadBounded(contents, MaximumEntryBytes) is not { } bytes)
                {
                    return new ArchiveContents(
                        $"'{name}' pesa más de {MaximumEntryBytes / (1024 * 1024)} MB una vez "
                            + "descomprimido.",
                        [], []);
                }

                total += bytes.Length;

                if (total > MaximumTotalBytes)
                {
                    return new ArchiveContents(
                        $"El comprimido supera los {MaximumTotalBytes / (1024 * 1024)} MB una vez "
                            + "descomprimido.",
                        [], []);
                }

                photos.Add(new ArchivedPhoto(
                    name, MatchKey(Path.GetFileNameWithoutExtension(name)), bytes));
            }

            return new ArchiveContents(null, photos, ignored);
        }
    }

    /// <summary>
    /// A name reduced to what is compared against an identity document.
    /// </summary>
    /// <remarks>
    /// Letters and digits only, lowercased, so <c>V-101.jpg</c>,
    /// <c>v 101.JPG</c> and <c>v101.jpeg</c> all find the same person. The
    /// generosity is deliberate: the operator renaming four hundred files is
    /// the slowest part of this whole flow, and refusing one because of a
    /// hyphen would be pedantry with a real cost.
    /// </remarks>
    public static string MatchKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>
    /// Reads a stream up to a ceiling, or nothing if it goes past it.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>CopyTo</c> into a buffer sized from
    /// <see cref="ZipArchiveEntry.Length"/>. That number comes out of the
    /// archive's own directory and is whatever the person who built the
    /// archive put there — a file claiming to be one kilobyte may decompress
    /// forever, and the only honest limit is on what is actually read.
    /// </remarks>
    private static byte[]? ReadBounded(Stream source, int limit)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        while (true)
        {
            var read = source.Read(chunk, 0, chunk.Length);

            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }
}
