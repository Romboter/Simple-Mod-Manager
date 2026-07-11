using System.IO;

namespace VintageStoryModManager.Helpers;

internal static class PathRelationshipHelper
{
    internal static bool IsPathUnderDirectory(string path, string? directory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory)) return false;

            try
            {
                var normalizedPath = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedDirectory = Path.GetFullPath(directory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (normalizedPath.Length < normalizedDirectory.Length) return false;

                if (!normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase)) return false;

                if (normalizedPath.Length == normalizedDirectory.Length) return true;

                var separator = normalizedPath[normalizedDirectory.Length];
                return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar;
            }
            catch (Exception)
            {
                return false;
            }
        }

    internal static bool IsSameDirectory(string? first, string? second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;

            try
            {
                var normalizedFirst = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
                var normalizedSecond = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return string.Equals(normalizedFirst, normalizedSecond, comparison);
            }
            catch
            {
                return false;
            }
        }

    internal static bool IsPathWithinDirectory(string directory, string candidatePath)
        {
            try
            {
                var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))
                                          + Path.DirectorySeparatorChar;
                var normalizedPath = Path.GetFullPath(candidatePath);
                return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
}
