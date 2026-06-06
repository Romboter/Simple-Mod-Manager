using System.IO;
using System.Text;

namespace VintageStoryModManager.Helpers;

internal static class ModConfigPathHelper
{
    private static readonly string[] SupportedConfigExtensions =
    {
        ".json",
        ".yaml",
        ".yml"
    };

    internal static string? NormalizeRelativeConfigPath(string? relativePath, string sanitizedFileName)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var normalized = relativePath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            if (string.IsNullOrWhiteSpace(normalized)) return null;

            var directory = Path.GetDirectoryName(normalized);
            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = sanitizedFileName;

            return string.IsNullOrWhiteSpace(directory)
                ? fileName
                : Path.Combine(directory, fileName);
        }

    internal static string EnsureUniqueRelativePath(string relativePath, HashSet<string> usedRelativePaths)
        {
            var uniqueRelativePath = relativePath;
            var counter = 1;

            while (!usedRelativePaths.Add(uniqueRelativePath))
            {
                var directory = Path.GetDirectoryName(relativePath);
                var baseName = Path.GetFileNameWithoutExtension(relativePath);
                var extension = Path.GetExtension(relativePath);
                var uniqueFileName = $"{baseName}_{counter++}{extension}";
                uniqueRelativePath = string.IsNullOrWhiteSpace(directory)
                    ? uniqueFileName
                    : Path.Combine(directory, uniqueFileName);
            }

            return uniqueRelativePath;
        }

    internal static string[] GetSupportedConfigFiles(string directory)
        {
            return Directory
                .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(file => IsSupportedConfigExtension(Path.GetExtension(file)))
                .ToArray();
        }

    private static bool IsSupportedConfigExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;

            foreach (var supported in SupportedConfigExtensions)
                if (extension.Equals(supported, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

    internal static string GetSafeConfigFileName(string? candidate, string? modId)
        {
            var sanitizedModId = SanitizeForFileName(string.IsNullOrWhiteSpace(modId) ? "modconfig" : modId!.Trim()).Trim();
            if (string.IsNullOrWhiteSpace(sanitizedModId)) sanitizedModId = "modconfig";

            var trimmedCandidate = string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim();
            var extension = ResolveConfigExtension(trimmedCandidate);
            string baseName;

            if (string.IsNullOrWhiteSpace(trimmedCandidate))
            {
                baseName = sanitizedModId;
            }
            else
            {
                var candidateFileName = Path.GetFileName(trimmedCandidate);
                var candidateBase = string.IsNullOrWhiteSpace(candidateFileName)
                    ? null
                    : Path.GetFileNameWithoutExtension(candidateFileName);
                baseName = string.IsNullOrWhiteSpace(candidateBase) ? sanitizedModId : candidateBase!;
            }

            var sanitizedBase = SanitizeForFileName(baseName).Trim();
            if (string.IsNullOrWhiteSpace(sanitizedBase)) sanitizedBase = sanitizedModId;

            return sanitizedBase + extension;
        }

    private static string ResolveConfigExtension(string? candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                var extension = Path.GetExtension(candidate);
                if (IsSupportedConfigExtension(extension)) return extension;
            }

            return ".json";
        }

    private static string SanitizeForFileName(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);

            foreach (var ch in value) builder.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch);

            return builder.ToString();
        }
}
