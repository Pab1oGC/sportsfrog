using System.IO.Compression;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Checks that an uploaded workbook's real, decompressed size is bounded
/// before anything tries to fully parse it.
/// </summary>
/// <remarks>
/// An .xlsx is a zip archive, and ClosedXML's own reader has no ceiling on
/// what it will decompress: a file well under the 5 MB upload limit can
/// expand to hundreds of megabytes before <c>XLWorkbook</c> ever finishes
/// parsing it, and doing that on every upload attempt grows this process's
/// memory with no bound — confirmed directly: a 0.5 MB crafted file reached
/// past 1 GiB of process memory across a few repeated attempts, none of them
/// even accepted as a valid workbook.
///
/// The same defence <see cref="Athletes.Photos.PhotoArchive"/> already
/// applies to a batch of photos applies here: nothing trusts a declared
/// entry size (<see cref="ZipArchiveEntry.Length"/> is exactly what a
/// crafted file lies about), every entry is read through a ceiling, and a
/// running total across the archive stops the same trick spread over many
/// small entries.
///
/// A file that is not a valid zip at all is not this class's problem to
/// report: it is let through unchanged so <c>XLWorkbook</c>'s own broad
/// catch produces the one message every unreadable upload already gets,
/// rather than a second, slightly different way of saying the same thing.
/// </remarks>
internal static class WorkbookGuard
{
    /// <summary>
    /// Per part of the workbook, before anything is handed to the real
    /// parser. Generous on purpose: a workbook's own drawings or a pasted
    /// image can be a few megabytes long before this should ever refuse it.
    /// </summary>
    private const long MaximumEntryBytes = 50L * 1024 * 1024;

    /// <summary>
    /// Across the whole workbook, decompressed. A genuine roster — a few
    /// hundred rows of text — is kilobytes; this ceiling exists for the
    /// crafted case, not the ordinary one.
    /// </summary>
    private const long MaximumTotalBytes = 100L * 1024 * 1024;

    /// <summary>Past any real workbook's number of internal parts.</summary>
    private const int MaximumEntries = 200;

    /// <summary>
    /// Null when the file is safe to hand to <c>XLWorkbook</c> — including
    /// when it is not a zip at all, which is that reader's own failure to
    /// explain — or the reason it is not.
    /// </summary>
    /// <remarks>
    /// Rewinds <paramref name="file"/> to the beginning before returning
    /// either way, so the caller's own attempt to open it as a workbook
    /// starts from byte zero regardless of how far this got.
    /// </remarks>
    public static string? Inspect(Stream file)
    {
        ZipArchive archive;

        try
        {
            archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            file.Position = 0;
            return null;
        }

        using (archive)
        {
            if (archive.Entries.Count > MaximumEntries)
            {
                file.Position = 0;

                return $"El archivo tiene más de {MaximumEntries} partes internas, y ningún .xlsx "
                    + "real llega a tantas.";
            }

            long total = 0;

            foreach (var entry in archive.Entries)
            {
                using var contents = entry.Open();
                var chunk = new byte[81920];
                long entryTotal = 0;

                while (true)
                {
                    // Read, never the declared Length: that number comes out
                    // of the archive's own directory and is whatever the
                    // person who built it put there.
                    var read = contents.Read(chunk, 0, chunk.Length);

                    if (read == 0)
                    {
                        break;
                    }

                    entryTotal += read;
                    total += read;

                    if (entryTotal > MaximumEntryBytes || total > MaximumTotalBytes)
                    {
                        file.Position = 0;

                        return "El archivo pesa demasiado una vez descomprimido para ser una "
                            + "planilla real. Tiene que ser el .xlsx que genera el sistema.";
                    }
                }
            }
        }

        file.Position = 0;
        return null;
    }
}
