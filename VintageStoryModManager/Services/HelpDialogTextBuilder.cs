namespace VintageStoryModManager.Services;

internal static class HelpDialogTextBuilder
{
    internal static string BuildOpenDiscordFailureMessage(string errorMessage)
    {
        return $"Failed to open Discord:\n{errorMessage}";
    }
}
