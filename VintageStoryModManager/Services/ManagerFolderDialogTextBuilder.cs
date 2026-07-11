#nullable enable

namespace VintageStoryModManager.Services;

internal static class ManagerFolderDialogTextBuilder
{
    internal const string ManagerDataFolderUnavailableMessage =
        "The manager data folder is not available on this system.";

    internal const string CannotDetermineCurrentManagerFolderMessage =
        "Cannot determine the current manager data folder location.";

    internal const string CannotDetermineDefaultManagerFolderMessage =
        "Cannot determine the default manager data folder location.";

    internal const string SameLocationMessage =
        "The selected location is the same as the current location.";

    internal static string BuildOpenManagerFolderFailureMessage(string? errorMessage)
    {
        return $"Failed to open the manager data folder:\n{errorMessage}";
    }

    internal static string BuildExistingFolderPrompt(string folder)
    {
        return $"The folder \"{folder}\" already exists.\n\n" +
               "Do you want to merge with the existing folder?\n" +
               "(Existing files with the same name will be overwritten)";
    }

    internal static string BuildMoveConfirmationMessage(string currentFolder, string newManagerFolder)
    {
        return $"Move manager folder from:\n{currentFolder}\n\n" +
               $"To:\n{newManagerFolder}\n\n" +
               "The application will restart after the move is complete.\n\n" +
               "Continue?";
    }

    internal static string BuildMoveSuccessMessage(string newManagerFolder)
    {
        return $"Manager folder moved successfully to:\n{newManagerFolder}\n\n" +
               "The application will now restart.";
    }

    internal static string BuildMoveFailureMessage(string? errorMessage)
    {
        return $"Failed to move the manager folder:\n\n{errorMessage}";
    }

    internal static string BuildAlreadyDefaultMessage(string defaultFolder)
    {
        return $"Already using the default manager folder location:\n{defaultFolder}";
    }

    internal static string BuildResetConfirmationMessage(string currentFolder, string defaultFolder)
    {
        return "Reset manager folder to default location?\n\n" +
               $"Current location:\n{currentFolder}\n\n" +
               $"Default location:\n{defaultFolder}\n\n" +
               "The manager will:\n" +
               "• Move all configuration files, cached mods, backups, and presets\n" +
               "• Update the configuration to use the default location\n" +
               "• Require a restart to complete the change\n\n" +
               "Note: The Firebase authentication backup (SVSM Backup folder) " +
               "will remain in its original location.\n\n" +
               "Continue?";
    }

    internal static string BuildResetSuccessMessage(string defaultFolder)
    {
        return $"Manager folder reset to default location:\n{defaultFolder}\n\n" +
               "The application will now restart.";
    }

    internal static string BuildResetFailureMessage(string? errorMessage)
    {
        return $"Failed to reset the manager folder:\n\n{errorMessage}";
    }
}
