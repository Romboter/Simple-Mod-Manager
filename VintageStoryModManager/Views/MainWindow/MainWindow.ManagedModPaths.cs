#nullable enable

using System;
using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private bool TryGetManagedModPath(ModListItemViewModel mod, out string fullPath, out string? errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = null;

        if (_dataDirectory is null)
        {
            errorMessage =
                "The VintagestoryData folder is not available. Please verify it from File > Set Data Folder.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(mod.SourcePath))
        {
            errorMessage = "This mod does not have a known source path and cannot be deleted automatically.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(mod.SourcePath);
        }
        catch (Exception)
        {
            errorMessage = "The mod path is invalid and cannot be deleted automatically.";
            return false;
        }

        if (!IsPathWithinManagedMods(fullPath))
        {
            errorMessage =
                $"This mod is located outside of the Mods folder and cannot be deleted automatically.{Environment.NewLine}{Environment.NewLine}Location:{Environment.NewLine}{fullPath}";
            return false;
        }

        if (!TryEnsureManagedModTargetIsSafe(fullPath, out errorMessage)) return false;

        return true;
    }

    private bool TryGetDependencyInstallTargetPath(string modId, ModReleaseInfo release, out string fullPath,
        out string? errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = null;

        if (_dataDirectory is null)
        {
            errorMessage =
                "The VintagestoryData folder is not available. Please verify it from File > Set Data Folder.";
            return false;
        }

        var modsDirectory = Path.Combine(_dataDirectory, "Mods");

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

    private bool IsPathWithinManagedMods(string fullPath)
    {
        if (_dataDirectory is null) return false;

        var modsDirectory = Path.Combine(_dataDirectory, "Mods");
        var modsByServerDirectory = Path.Combine(_dataDirectory, "ModsByServer");
        return PathRelationshipHelper.IsPathUnderDirectory(fullPath, modsDirectory)
               || PathRelationshipHelper.IsPathUnderDirectory(fullPath, modsByServerDirectory);
    }

    private bool TryEnsureManagedModTargetIsSafe(string fullPath, out string? errorMessage)
    {
        errorMessage = null;

        FileSystemInfo? info = null;

        if (Directory.Exists(fullPath))
            info = new DirectoryInfo(fullPath);
        else if (File.Exists(fullPath)) info = new FileInfo(fullPath);

        if (info is null) return true;

        if (!info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;

        try
        {
            var target = info.ResolveLinkTarget(true);

            if (target is null)
            {
                errorMessage =
                    $"This mod is a symbolic link and its target could not be resolved. It will not be deleted automatically.{Environment.NewLine}{Environment.NewLine}Location:{Environment.NewLine}{fullPath}";
                return false;
            }

            var resolvedFullPath = Path.GetFullPath(target.FullName);

            if (!IsPathWithinManagedMods(resolvedFullPath))
            {
                errorMessage =
                    $"This mod is a symbolic link that points outside of the Mods folder and cannot be deleted automatically.{Environment.NewLine}{Environment.NewLine}Link target:{Environment.NewLine}{resolvedFullPath}";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or PlatformNotSupportedException)
        {
            errorMessage =
                $"This mod is a symbolic link that could not be validated for automatic deletion.{Environment.NewLine}{Environment.NewLine}Location:{Environment.NewLine}{fullPath}{Environment.NewLine}{Environment.NewLine}Reason:{Environment.NewLine}{ex.Message}";
            return false;
        }
    }
}
