using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class CloudModlistCacheService
{
    internal static async Task<string> CacheAsync(
        string cacheDirectory,
        CloudModlistListEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(entry);

        var cacheFileName =
            FileNameHelper.BuildSuggestedFileName(
                entry.Name ?? entry.DisplayName,
                "Cloud Modlist");

        var cacheFilePath =
            FileNameHelper.GetUniqueFilePath(
                cacheDirectory,
                cacheFileName,
                ".json");

        await File.WriteAllTextAsync(
            cacheFilePath,
            entry.ContentJson);

        return cacheFilePath;
    }
}
