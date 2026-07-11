using System.IO;

namespace VintageStoryModManager.Services;

internal static class InstallationPathValidator
{
    internal static bool TryValidateDataDirectory(string? path, out string? normalizedPath, out string? errorMessage)
        {
            normalizedPath = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "No folder was selected.";
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                errorMessage = "The folder path is invalid.";
                return false;
            }

            if (!Directory.Exists(normalizedPath))
            {
                errorMessage = "The folder does not exist.";
                return false;
            }

            var hasClientSettings = File.Exists(Path.Combine(normalizedPath, "clientsettings.json"));
            var hasMods = Directory.Exists(Path.Combine(normalizedPath, "Mods"));
            var hasConfig = Directory.Exists(Path.Combine(normalizedPath, "ModConfig"));

            if (!hasClientSettings && !hasMods && !hasConfig)
            {
                errorMessage = "The folder does not appear to be a VintagestoryData directory.";
                return false;
            }

            return true;
        }

    internal static bool TryValidateGameDirectory(string? path, out string? normalizedPath, out string? errorMessage)
        {
            normalizedPath = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "No folder was selected.";
                return false;
            }

            string candidate;
            try
            {
                candidate = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                errorMessage = "The folder path is invalid.";
                return false;
            }

            if (File.Exists(candidate))
            {
                var directory = Path.GetDirectoryName(candidate);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    errorMessage = "The folder path is invalid.";
                    return false;
                }

                candidate = directory;
            }

            if (!Directory.Exists(candidate))
            {
                errorMessage = "The folder does not exist.";
                return false;
            }

            var executable = GameDirectoryLocator.FindExecutable(candidate);
            if (executable is null)
            {
                errorMessage = "The folder does not contain a Vintage Story executable.";
                return false;
            }

            normalizedPath = candidate;
            return true;
        }
}
