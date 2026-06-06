using System.IO;
using System.Text;

namespace VintageStoryModManager.Services;

internal static class ManagerCacheCleanupService
{
    internal static void ClearManagerCaches(bool preserveModCache)
    {
        var errors = new List<string>();

        try
        {
            ModManifestCacheService.ClearCache();
        }
        catch (Exception ex)
        {
            errors.Add(BuildCacheClearErrorMessage("Mod metadata cache", ex));
        }

        var cachedModsDirectory = ModCacheLocator.GetCachedModsDirectory();
        if (!preserveModCache && !string.IsNullOrWhiteSpace(cachedModsDirectory))
            try
            {
                ClearCachedModsDirectory(cachedModsDirectory);
            }
            catch (Exception ex)
            {
                errors.Add(BuildCacheClearErrorMessage($"Cached mods at {cachedModsDirectory}", ex));
            }

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine + Environment.NewLine, errors));
    }

    internal static CachedModsDeletionStatus DeleteCachedMods()
    {
        var cachedModsDirectory = ModCacheLocator.GetCachedModsDirectory();
        if (string.IsNullOrWhiteSpace(cachedModsDirectory))
            return CachedModsDeletionStatus.DirectoryUnavailable;

        if (!Directory.Exists(cachedModsDirectory))
            return CachedModsDeletionStatus.DirectoryNotFound;

        foreach (var directory in Directory.GetDirectories(cachedModsDirectory))
            Directory.Delete(directory, true);

        return CachedModsDeletionStatus.Deleted;
    }

    internal static ManagerCacheClearResult ClearAllManagerCacheFolders()
    {
        var managerDataDirectory = ModCacheLocator.GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(managerDataDirectory))
            return ManagerCacheClearResult.DataDirectoryUnavailable;

        var deletedFolders = new List<string>();
        var failedFolders = new List<string>();

        var cacheFolders = new[]
        {
            ("Temp Cache", Path.Combine(managerDataDirectory, "Temp Cache"))
        };

        foreach (var (name, path) in cacheFolders)
            try
            {
                if (!Directory.Exists(path)) continue;

                Directory.Delete(path, true);
                deletedFolders.Add(name);
            }
            catch (Exception ex)
            {
                failedFolders.Add($"{name}: {ex.Message}");
            }

        var legacyCacheFolders = new[]
        {
            Path.Combine(managerDataDirectory, "Mod Database Cache"),
            Path.Combine(managerDataDirectory, "Cached Mods"),
            Path.Combine(managerDataDirectory, "Mod Metadata"),
            Path.Combine(managerDataDirectory, "Firebase Cache")
        };

        foreach (var legacyPath in legacyCacheFolders)
            try
            {
                if (Directory.Exists(legacyPath))
                    Directory.Delete(legacyPath, true);
            }
            catch
            {
                // Silently ignore failures for legacy folders.
            }

        return new ManagerCacheClearResult(true, deletedFolders, failedFolders);
    }

    internal static long? GetCachedModsSize()
    {
        var cachedModsDirectory = ModCacheLocator.GetCachedModsDirectory();
        if (string.IsNullOrWhiteSpace(cachedModsDirectory) ||
            !Directory.Exists(cachedModsDirectory))
            return null;

        return DirectoryUtility.CalculateDirectorySize(cachedModsDirectory);
    }

    private static void ClearCachedModsDirectory(string cachedModsDirectory)
    {
        if (!Directory.Exists(cachedModsDirectory)) return;

        Directory.Delete(cachedModsDirectory, true);
    }

    private static string BuildCacheClearErrorMessage(string context, Exception ex)
    {
        var builder = new StringBuilder();
        builder.Append(context);
        builder.Append(':');
        builder.AppendLine();
        builder.Append(ex.Message);

        var inner = ex.InnerException;
        while (inner is not null)
        {
            builder.AppendLine();
            builder.Append(inner.Message);
            inner = inner.InnerException;
        }

        return builder.ToString();
    }
}

internal enum CachedModsDeletionStatus
{
    Deleted,
    DirectoryNotFound,
    DirectoryUnavailable
}

internal sealed record ManagerCacheClearResult(
    bool DataDirectoryAvailable,
    IReadOnlyList<string> DeletedFolders,
    IReadOnlyList<string> FailedFolders)
{
    internal static ManagerCacheClearResult DataDirectoryUnavailable { get; } =
        new(false, Array.Empty<string>(), Array.Empty<string>());
}
