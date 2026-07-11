#nullable enable

namespace VintageStoryModManager.Services;

internal static class ModlistDialogTextBuilder
{
    internal static string BuildReplaceExistingMessage(string fileName) =>
        $"A modlist named \"{fileName}\" already exists in the Modlists folder. Do you want to replace it?";

    internal static string BuildSaveFailureMessage(string errorMessage) =>
        $"Failed to save the modlist:\n{errorMessage}";

    internal static string BuildInvalidFileMessage(string? errorMessage)
    {
        var message = "The file is not a valid SVSM modlist.";
        if (!string.IsNullOrWhiteSpace(errorMessage)) message += $"\n{errorMessage}";
        return message;
    }
}
