#nullable enable

using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal static class ModInstallTargetPathHelper
{
    internal static bool TryGetInstallTargetPath(string? dataDirectory, ModListItemViewModel mod,
        ModReleaseInfo release, out string fullPath, out string? errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = null;

        if (dataDirectory is null)
        {
            errorMessage =
                "The VintagestoryData folder is not available. Please verify it from File > Set Data Folder.";
            return false;
        }

        var modsDirectory = Path.Combine(dataDirectory, "Mods");

        try
        {
            Directory.CreateDirectory(modsDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            errorMessage = $"The Mods folder could not be accessed:{Environment.NewLine}{ex.Message}";
            return false;
        }

        var defaultName = string.IsNullOrWhiteSpace(mod.ModId) ? "mod" : mod.ModId;
        var versionPart = string.IsNullOrWhiteSpace(release.Version) ? "latest" : release.Version!;
        var fallbackFileName = $"{defaultName}-{versionPart}.zip";

        var releaseFileName = release.FileName;
        if (!string.IsNullOrWhiteSpace(releaseFileName)) releaseFileName = Path.GetFileName(releaseFileName);

        var sanitizedFileName = FileNameHelper.SanitizeFileName(releaseFileName, fallbackFileName);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(sanitizedFileName))) sanitizedFileName += ".zip";

        var candidatePath = Path.Combine(modsDirectory, sanitizedFileName);
        fullPath = FileNameHelper.EnsureUniqueFilePath(candidatePath);
        return true;
    }

    internal static bool TryGetDependencyInstallTargetPath(string? dataDirectory, string modId,
        ModReleaseInfo release, out string fullPath, out string? errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = null;

        if (dataDirectory is null)
        {
            errorMessage =
                "The VintagestoryData folder is not available. Please verify it from File > Set Data Folder.";
            return false;
        }

        var modsDirectory = Path.Combine(dataDirectory, "Mods");

        try
        {
            Directory.CreateDirectory(modsDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            errorMessage = $"The Mods folder could not be accessed:{Environment.NewLine}{ex.Message}";
            return false;
        }

        var defaultName = string.IsNullOrWhiteSpace(modId) ? "mod" : modId;
        var versionPart = string.IsNullOrWhiteSpace(release.Version) ? "latest" : release.Version!;
        var fallbackFileName = $"{defaultName}-{versionPart}.zip";

        var releaseFileName = release.FileName;
        if (!string.IsNullOrWhiteSpace(releaseFileName)) releaseFileName = Path.GetFileName(releaseFileName);

        var sanitizedFileName = FileNameHelper.SanitizeFileName(releaseFileName, fallbackFileName);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(sanitizedFileName))) sanitizedFileName += ".zip";

        var candidatePath = Path.Combine(modsDirectory, sanitizedFileName);
        fullPath = FileNameHelper.EnsureUniqueFilePath(candidatePath);
        return true;
    }
}
