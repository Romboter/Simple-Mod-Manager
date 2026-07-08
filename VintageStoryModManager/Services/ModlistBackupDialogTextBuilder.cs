namespace VintageStoryModManager.Services;

internal static class ModlistBackupDialogTextBuilder
{
    internal const string SelectedBackupMissingMessage =
        "The selected backup could not be found.";

    internal static string BuildRestoreFailureMessage(string? errorMessage)
    {
        var message = string.IsNullOrWhiteSpace(errorMessage)
            ? "The selected backup is not valid."
            : errorMessage!;

        return $"Failed to restore the backup:\n{message}";
    }

    internal static string BuildRestoredStatusMessage(string? backupName)
    {
        return $"Restored backup \"{backupName}\".";
    }
}
