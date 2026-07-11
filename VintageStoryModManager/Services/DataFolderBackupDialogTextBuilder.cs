namespace VintageStoryModManager.Services;

internal static class DataFolderBackupDialogTextBuilder
{
    internal const string DataDirectoryUnavailableForRestoreMessage =
        "The VintagestoryData folder is not available. Please set it before restoring a backup.";

    internal const string RestoreConfirmationMessage =
        "Restoring a VintagestoryData backup replaces the entire folder (the Cache folder will be cleared). Continue?";

    internal const string SetDataDirectoryBeforeDeleteMessage =
        "Set the VintagestoryData folder before deleting backups.";

    internal const string UnknownInstalledVersionDeleteMessage =
        "The installed Vintage Story version could not be determined, so backups cannot be deleted safely.";

    internal const string NoMatchingBackupsMessage =
        "No backups matching the current data folder and Vintage Story version were found.";

    internal const string DifferentDataFolderRestoreMessage =
        "This backup was created for a different VintagestoryData folder and cannot be restored.";

    internal const string RestoreSuccessMessage =
        "VintagestoryData was restored from the selected backup.";

    internal const string BackupDirectoryUnavailableMessage =
        "The data backup directory is not available.";

    internal static string BuildDeleteConfirmation(string displayVersion)
    {
        return $"Delete all VintagestoryData backups for Vintage Story {displayVersion}? This action cannot be undone.";
    }

    internal static string BuildDeletedStatusMessage(int deletedCount)
    {
        return deletedCount == 1
            ? "Deleted 1 VintagestoryData backup."
            : $"Deleted {deletedCount} VintagestoryData backups.";
    }

    internal static string BuildDeleteFailureMessage(string errorMessage)
    {
        return $"Failed to delete the VintagestoryData backups:\n{errorMessage}";
    }

    internal static string BuildVersionMismatchMessage(
        string backupVersionDisplay,
        string installedVersionDisplay)
    {
        return $"This backup was created for Vintage Story {backupVersionDisplay}, but the installed version is {installedVersionDisplay}. Install the matching Vintage Story version before restoring this backup.";
    }

    internal static string BuildRestoreFailureMessage(string errorMessage)
    {
        return $"Failed to restore the selected VintagestoryData backup:\n{errorMessage}";
    }

    internal static string BuildOpenBackupDirectoryFailureMessage(string errorMessage)
    {
        return $"Failed to open the data backup directory:\n{errorMessage}";
    }

    internal static string BuildChangeBackupLocationFailureMessage(string errorMessage)
    {
        return $"Failed to set the backup location:\n{errorMessage}";
    }
}
