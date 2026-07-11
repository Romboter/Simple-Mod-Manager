#nullable enable

using System.IO;

namespace VintageStoryModManager.Services;

internal static class PdfDialogTextBuilder
{
    internal const string ModsStillLoadingMessage =
        "Mods are still loading. Please try again once loading is complete.";

    internal const string NoInstalledModsMessage =
        "No installed mods were found to include in the PDF.";

    internal const string SavedSuccessfullyMessage =
        "Saved installed mods PDF successfully.";

    internal static string BuildReplaceExistingMessage(string filePath)
    {
        return $"A modlist PDF named \"{Path.GetFileName(filePath)}\" already exists in the Modlists folder. Do you want to replace it?";
    }

    internal static string BuildPrepareFolderFailureMessage(string? errorMessage)
    {
        return $"Failed to prepare the Modlists folder:\n{errorMessage}";
    }

    internal static string BuildSaveFailureMessage(string? errorMessage)
    {
        return $"Failed to save the PDF:\n{errorMessage}";
    }

    internal static string BuildGenerateFailureMessage(string? errorMessage)
    {
        return $"Failed to generate the PDF:\n{errorMessage}";
    }
}
