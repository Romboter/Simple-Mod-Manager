#nullable enable

using System.Globalization;

namespace VintageStoryModManager.Services;

internal static class CompatibilityDialogTextBuilder
{
    internal static string BuildFailedToRetrieveVersionsMessage(string? errorMessage)
    {
        return $"Failed to retrieve Vintage Story versions:\n{errorMessage}";
    }

    internal static string BuildNoInstalledModsMessage(string targetVersion)
    {
        return $"Vintage Story version: {targetVersion}.\n\nNo installed mods were found.";
    }

    internal static string BuildExperimentalCompReviewFailedMessage(string? errorMessage)
    {
        return $"The experimental compatibility review failed:\n{errorMessage}";
    }

    internal static string BuildCompatibilityCommentsTitle(string? displayName)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            "Compatibility comments for {0}",
            displayName);
    }
}
