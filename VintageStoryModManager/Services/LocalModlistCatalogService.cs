using System.IO;
using System.Text;
using UglyToad.PdfPig;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class LocalModlistCatalogService
{
    internal static LocalModlistCatalogResult BuildCatalog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
            return LocalModlistCatalogResult.Empty;

        var entries = new List<LocalModlistListEntry>();
        var errors = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (!ModlistDropHelper.HasSupportedModlistExtension(filePath))
                continue;

            if (TryCreateEntry(filePath, out var entry, out var error))
            {
                if (entry is not null)
                    entries.Add(entry);

                continue;
            }

            if (!string.IsNullOrWhiteSpace(error))
                errors.Add($"{Path.GetFileName(filePath)}: {error}");
        }

        entries.Sort((left, right) =>
        {
            var comparison = string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);

            if (comparison != 0)
                return comparison;

            return string.Compare(
                left.FilePath,
                right.FilePath,
                StringComparison.OrdinalIgnoreCase);
        });

        return new LocalModlistCatalogResult(entries, errors);
    }

    private static bool TryCreateEntry(
        string filePath,
        out LocalModlistListEntry? entry,
        out string? error)
    {
        entry = null;
        error = null;

        try
        {
            if (string.Equals(
                    Path.GetExtension(filePath),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                using var document = PdfDocument.Open(filePath);
                string? json = null;

                var information = document.Information;
                var hasMetadata =
                    PdfModlistSerializer.TryExtractModlistJsonFromMetadata(
                        information?.Subject,
                        out json,
                        out var metadataError);

                if (!hasMetadata)
                {
                    if (metadataError is not null)
                    {
                        error = metadataError;
                        return false;
                    }

                    var textBuilder = new StringBuilder();

                    foreach (var page in document.GetPages())
                    {
                        var pageBuilder = new StringBuilder();

                        foreach (var letter in page.Letters)
                        {
                            var value = letter.Value;
                            if (string.IsNullOrEmpty(value))
                                continue;

                            if (value == "\r")
                                continue;

                            pageBuilder.Append(value);
                        }

                        var pageText = pageBuilder.ToString();
                        if (string.IsNullOrWhiteSpace(pageText))
                            continue;

                        if (textBuilder.Length > 0)
                            textBuilder.Append('\n');

                        textBuilder.Append(pageText);
                    }

                    var pdfText = textBuilder.ToString();
                    if (string.IsNullOrWhiteSpace(pdfText))
                    {
                        error = "The PDF did not contain any readable text.";
                        return false;
                    }

                    if (!PdfModlistSerializer.TryExtractModlistJson(
                            pdfText,
                            out json,
                            out var extractionError))
                    {
                        error = extractionError;
                        return false;
                    }
                }

                return TryCreateEntryFromJson(
                    filePath,
                    json!,
                    out entry,
                    out error);
            }

            var jsonText = File.ReadAllText(filePath);

            return TryCreateEntryFromJson(
                filePath,
                jsonText,
                out entry,
                out error);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryCreateEntryFromJson(
        string filePath,
        string json,
        out LocalModlistListEntry? entry,
        out string? error)
    {
        entry = null;
        error = null;

        if (!PdfModlistSerializer.TryDeserializeFromJson(
                json,
                out var preset,
                out var errorMessage))
        {
            error = string.IsNullOrWhiteSpace(errorMessage)
                ? "The file is not a valid SVSM modlist."
                : errorMessage;

            return false;
        }

        var metadata =
            ModlistMetadataParser.ExtractModlistMetadata(json);

        var mods = metadata.Mods ?? Array.Empty<string>();
        var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);

        DateTimeOffset? lastModified =
            lastWriteUtc == DateTime.MinValue
                ? null
                : new DateTimeOffset(lastWriteUtc, TimeSpan.Zero);

        var name = metadata.Name ?? preset?.Name;
        var description = metadata.Description ?? preset?.Description;
        var version = metadata.Version ?? preset?.Version;
        var uploader = metadata.Uploader ?? preset?.Uploader;
        var gameVersion = metadata.GameVersion ?? preset?.GameVersion;

        entry = new LocalModlistListEntry(
            filePath,
            name,
            description,
            version,
            uploader,
            mods,
            lastModified,
            gameVersion);

        return true;
    }
}

internal sealed record LocalModlistCatalogResult(
    IReadOnlyList<LocalModlistListEntry> Entries,
    IReadOnlyList<string> Errors)
{
    internal static LocalModlistCatalogResult Empty { get; } =
        new(
            Array.Empty<LocalModlistListEntry>(),
            Array.Empty<string>());
}
