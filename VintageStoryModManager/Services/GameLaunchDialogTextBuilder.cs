#nullable enable

namespace VintageStoryModManager.Services;

internal static class GameLaunchDialogTextBuilder
{
    internal static string BuildBackupFailedPromptMessage(string? errorMessage)
    {
        return $"The automatic VintagestoryData backup failed:\n{errorMessage}\n\nLaunch Vintage Story without creating a backup?";
    }

    internal static string BuildBackupFailedMessage(string? errorMessage)
    {
        return $"The automatic VintagestoryData backup failed:\n{errorMessage}";
    }

    internal static string BuildShortcutLaunchFailedMessage(string? errorMessage)
    {
        return $"Failed to launch Vintage Story using the shortcut:\n{errorMessage}";
    }

    internal static string BuildLaunchFailedMessage(string? errorMessage)
    {
        return $"Failed to launch Vintage Story:\n{errorMessage}";
    }

    internal static string BuildInvalidShortcutMessage(string? errorMessage)
    {
        return $"The selected shortcut is not valid:\n{errorMessage}";
    }
}
