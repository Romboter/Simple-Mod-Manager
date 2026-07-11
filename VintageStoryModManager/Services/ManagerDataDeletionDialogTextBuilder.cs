#nullable enable

using System.Collections.Generic;
using System.Text;

namespace VintageStoryModManager.Services;

internal static class ManagerDataDeletionDialogTextBuilder
{
    internal const string DeleteAllManagerFilesConfirmationMessage =
        "This will move every file Simple VS Manager created to the Recycle Bin, including its configuration folder, any ModData backups, cached mods, presets, and Firebase authentication tokens.\n\n" +
        "You can restore them from the Recycle Bin if needed. Continue?";

    internal const string NoManagerFilesFoundMessage =
        "No Simple VS Manager files were found.";

    internal const string FinishedMovingManagerFilesMessage =
        "Finished moving Simple VS Manager files to the Recycle Bin.";

    internal static string BuildDeletionResultMessage(
        IReadOnlyCollection<string> deletedPaths,
        IReadOnlyCollection<string> failedPaths)
    {
        var builder = new StringBuilder();

        if (deletedPaths.Count > 0)
        {
            builder.AppendLine("Moved the following locations to the Recycle Bin:");
            foreach (var path in deletedPaths) builder.AppendLine($"• {path}");
        }

        if (failedPaths.Count == 0)
            return builder.Length > 0
                ? builder.ToString()
                : FinishedMovingManagerFilesMessage;

        if (builder.Length > 0) builder.AppendLine();

        builder.AppendLine(
            "The following locations could not be moved to the Recycle Bin. Please remove them manually:");
        foreach (var path in failedPaths) builder.AppendLine($"• {path}");

        return builder.ToString();
    }
}
