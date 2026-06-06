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
