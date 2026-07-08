namespace VintageStoryModManager.Services;

internal static class CloudManagementDialogTextBuilder
{
    internal const string NoCloudModlistsSavedMessage =
        "You do not have any cloud modlists saved.";

    internal const string ContentUnavailableMessage =
        "The selected cloud modlist could not be loaded.";

    internal const string DeletedAllCloudModlistsAndAuthorizationMessage =
        "Cloud modlists and Firebase authorization have been deleted.";

    internal static string BuildRenameLoadFailureMessage(string? errorMessage)
    {
        return $"Failed to load the cloud modlist before renaming:\n{errorMessage}";
    }

    internal static string BuildInvalidRenameContentMessage(string? errorMessage)
    {
        return $"The cloud modlist data is invalid and could not be renamed:\n{errorMessage}";
    }

    internal static string BuildRenameSaveFailureMessage(string? errorMessage)
    {
        return $"Failed to rename the cloud modlist:\n{errorMessage}";
    }

    internal static string BuildDeleteFailureMessage(string? errorMessage)
    {
        return $"Failed to delete the cloud modlist:\n{errorMessage}";
    }

    internal static string BuildRenamedStatusMessage(string slotLabel, string? name)
    {
        return $"Renamed cloud modlist in {slotLabel} to \"{name}\".";
    }

    internal static string BuildDeletedStatusMessage(string slotLabel)
    {
        return $"Deleted cloud modlist from {slotLabel}.";
    }
}
