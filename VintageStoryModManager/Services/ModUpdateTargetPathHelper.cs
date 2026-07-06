#nullable enable

using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal static class ModUpdateTargetPathHelper
{
    internal static bool TryGetUpdateTargetPath(
        ModListItemViewModel mod,
        ModReleaseInfo release,
        string existingPath,
        out string fullPath,
        out string? errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(existingPath))
        {
            errorMessage = "The mod path could not be determined.";
            return false;
        }

        string? directory;
        try
        {
            directory = Path.GetDirectoryName(existingPath);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            errorMessage = $"The mod path is invalid:{Environment.NewLine}{ex.Message}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            errorMessage = "The mod path could not be determined.";
            return false;
        }

        var defaultName = string.IsNullOrWhiteSpace(mod.ModId) ? "mod" : mod.ModId;
        var versionPart = string.IsNullOrWhiteSpace(release.Version) ? "latest" : release.Version!;
        var fallbackFileName = $"{defaultName}-{versionPart}.zip";

        var releaseFileName = release.FileName;
        if (!string.IsNullOrWhiteSpace(releaseFileName)) releaseFileName = Path.GetFileName(releaseFileName);

        var sanitizedFileName = FileNameHelper.SanitizeFileName(releaseFileName, fallbackFileName);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(sanitizedFileName))) sanitizedFileName += ".zip";

        fullPath = Path.Combine(directory, sanitizedFileName);
        return true;
    }

    internal static bool TryResolveUpdateTarget(
        ModListItemViewModel mod,
        ModReleaseInfo release,
        string sourcePath,
        out string targetPath,
        out bool targetIsDirectory,
        out string? existingPath,
        out string? errorMessage)
    {
        existingPath = null;
        errorMessage = null;
        targetPath = sourcePath;

        targetIsDirectory = Directory.Exists(sourcePath);
        if (!targetIsDirectory && !File.Exists(sourcePath) && mod.SourceKind == ModSourceKind.Folder)
            targetIsDirectory = true;

        if (targetIsDirectory) return true;

        if (!TryGetUpdateTargetPath(mod, release, sourcePath, out var resolvedPath, out errorMessage))
            return false;

        targetPath = resolvedPath;
        existingPath = sourcePath;
        return true;
    }
}
