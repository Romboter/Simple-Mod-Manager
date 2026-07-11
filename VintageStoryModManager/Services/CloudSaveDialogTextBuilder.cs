namespace VintageStoryModManager.Services;

internal static class CloudSaveDialogTextBuilder
{
    internal static string BuildReplaceExistingPrompt(string modlistName, string slotLabel)
    {
        return $"A cloud modlist named \"{modlistName}\" already exists in {slotLabel}. Do you want to replace it?";
    }

    internal static string BuildReplacedStatusMessage(string replacedName, string modlistName)
    {
        return $"Replaced cloud modlist \"{replacedName}\" with \"{modlistName}\".";
    }

    internal static string BuildSavedStatusMessage(string modlistName)
    {
        return $"Saved cloud modlist \"{modlistName}\" to the cloud.";
    }
}
