using System.IO;
using System.Text;

namespace VintageStoryModManager.Helpers;

internal static class FileNameHelper
{
    internal static string SanitizeFileName(string? fileName, string fallback)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);

            foreach (var c in name) builder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);

            var sanitized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }

    internal static string EnsureUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;

            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            if (string.IsNullOrWhiteSpace(directory)) directory = Directory.GetCurrentDirectory();

            var counter = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, $"{fileName} ({counter}){extension}");
                counter++;
            } while (File.Exists(candidate));

            return candidate;
        }

    internal static string EnsureUniqueDirectoryPath(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return path;

            var basePath = path;
            var counter = 1;
            string candidate;
            do
            {
                candidate = $"{basePath} ({counter++})";
            } while (Directory.Exists(candidate) || File.Exists(candidate));

            return candidate;
        }

    internal static string BuildSuggestedFileName(string? name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var ch in name) builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);

            var sanitized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }

    internal static string GetUniqueFilePath(string directory, string baseFileName, string extension)
        {
            var safeBaseName = string.IsNullOrWhiteSpace(baseFileName) ? "Modlist" : baseFileName;
            var fileName = safeBaseName + extension;
            var path = Path.Combine(directory, fileName);
            var counter = 1;

            while (File.Exists(path))
            {
                fileName = $"{safeBaseName} ({counter}){extension}";
                path = Path.Combine(directory, fileName);
                counter++;
            }

            return path;
        }

    internal static string GetSnapshotNameFromFilePath(string filePath, string fallback)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        }
}
