#nullable enable

using System.Collections.Generic;
using System.Text;

namespace VintageStoryModManager.Services;

internal static class ManagerCacheDialogTextBuilder
{
    internal const string DeleteCachedModsConfirmationMessage =
        "This will only delete the managers cached mods to save some disk space, it will not affect your installed mods.";

    internal const string CachedModsDirectoryUnavailableMessage =
        "Could not determine the cached mods directory.";

    internal const string NoCachedModsFoundMessage =
        "No cached mods were found.";

    internal const string CachedModsDeletedSuccessfullyMessage =
        "Cached mods deleted successfully.";

    internal const string ClearAllCachesConfirmationMessage =
        "This will delete all cache folders used by Simple VS Manager:\n\n" +
        "• Temp Cache (contains all cache data)\n\n" +
        "Your settings, modlists and installed mods will NOT be affected.\n\n" +
        "This is useful when experiencing problems with the mod database or cached data.\n\n" +
        "Continue?";

    internal const string ManagerDataDirectoryUnavailableMessage =
        "Could not locate the Simple VS Manager data directory.";

    internal static string BuildDeleteCachedModsFailureMessage(string? errorMessage)
    {
        return $"Failed to delete cached mods:\n{errorMessage}";
    }

    internal static string BuildClearAllCachesResultMessage(
        IReadOnlyCollection<string> deletedFolders,
        IReadOnlyCollection<string> failedFolders)
    {
        var messageBuilder = new StringBuilder();

        if (deletedFolders.Count > 0)
        {
            messageBuilder.AppendLine("Successfully deleted the following cache folders:");
            foreach (var folder in deletedFolders)
                messageBuilder.AppendLine($"• {folder}");
        }
        else if (failedFolders.Count == 0)
        {
            messageBuilder.AppendLine("No cache folders were found to delete.");
        }

        if (failedFolders.Count > 0)
        {
            if (messageBuilder.Length > 0) messageBuilder.AppendLine();

            messageBuilder.AppendLine("Failed to delete the following cache folders:");
            foreach (var error in failedFolders)
                messageBuilder.AppendLine($"• {error}");
        }

        return messageBuilder.ToString();
    }

    internal static string BuildClearAllCachesFailureMessage(string? errorMessage)
    {
        return $"An error occurred while clearing caches:\n\n{errorMessage}";
    }
}
