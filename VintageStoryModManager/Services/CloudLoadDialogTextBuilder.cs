namespace VintageStoryModManager.Services;

internal static class CloudLoadDialogTextBuilder
{
    internal const string NoCloudModlistsAvailableMessage =
        "No cloud modlists are available.";

    internal const string SelectedCloudModlistEmptyMessage =
        "The selected cloud modlist is empty.";

    internal const string NoCloudModlistsAvailableToDeleteMessage =
        "No cloud modlists are available to delete.";

    internal const string SelectedCloudModlistDownloadFailedMessage =
        "The selected cloud modlist could not be downloaded.";

    internal static string BuildCachePreparationFailureMessage(string errorMessage)
    {
        return $"Failed to prepare the cloud modlist cache:\n{errorMessage}";
    }

    internal static string BuildSelectedModlistCacheFailureMessage(string errorMessage)
    {
        return $"Failed to cache the selected modlist:\n{errorMessage}";
    }

    internal static string BuildDownloadedModlistLoadFailureMessage(string? errorMessage)
    {
        return string.IsNullOrWhiteSpace(errorMessage)
            ? "Failed to load the downloaded cloud modlist."
            : errorMessage!;
    }

    internal static string BuildCloudModlistLoadFailureMessage(string? errorMessage)
    {
        var message = string.IsNullOrWhiteSpace(errorMessage)
            ? "The selected cloud modlist is not valid."
            : errorMessage!;

        return $"Failed to load the modlist:\n{message}";
    }

    internal static string BuildDeleteConfirmationMessage(string displayName)
    {
        return $"Are you sure you want to delete {displayName}? This action cannot be undone.";
    }
}
