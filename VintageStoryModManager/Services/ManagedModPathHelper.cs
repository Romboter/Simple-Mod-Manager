#nullable enable

using System.IO;
using VintageStoryModManager.Helpers;

namespace VintageStoryModManager.Services;

internal static class ManagedModPathHelper
{
    internal static bool IsPathWithinManagedMods(string? dataDirectory, string fullPath)
    {
        if (dataDirectory is null) return false;

        var modsDirectory = Path.Combine(dataDirectory, "Mods");
        var modsByServerDirectory = Path.Combine(dataDirectory, "ModsByServer");
        return PathRelationshipHelper.IsPathUnderDirectory(fullPath, modsDirectory)
               || PathRelationshipHelper.IsPathUnderDirectory(fullPath, modsByServerDirectory);
    }

    internal static bool TryEnsureManagedModTargetIsSafe(string? dataDirectory, string fullPath,
        out string? errorMessage)
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

            if (!IsPathWithinManagedMods(dataDirectory, resolvedFullPath))
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
