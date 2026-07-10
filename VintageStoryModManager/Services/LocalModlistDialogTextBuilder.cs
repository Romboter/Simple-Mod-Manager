using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class LocalModlistDialogTextBuilder
{
    internal static string BuildFolderFailureMessage(string errorMessage) =>
        $"Failed to open the Modlists folder:\n{errorMessage}";

    internal static string BuildReadFailureMessage(string errorMessage) =>
        $"Failed to read local modlists:\n{errorMessage}";

    internal static string BuildDeleteConfirmation(IReadOnlyList<LocalModlistListEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 1)
        {
            var name = entries[0].DisplayName;
            return $"Are you sure you want to delete the modlist \"{name}\"? This cannot be undone.";
        }

        return $"Are you sure you want to delete the {entries.Count} selected modlists? This cannot be undone.";
    }

    internal static string BuildDeletionFailureMessage(IReadOnlyList<string> errors)
    {
        return "Some modlists could not be deleted:\n" + BuildErrorSummary(errors);
    }

    internal static string? BuildDeletionStatusMessage(int deletedCount)
    {
        if (deletedCount <= 0) return null;

        return deletedCount == 1
            ? "Deleted local modlist."
            : $"Deleted {deletedCount} local modlists.";
    }

    internal static string BuildCatalogFailureMessage(IReadOnlyList<string> errors)
    {
        return "Some local modlists could not be loaded:\n" + BuildErrorSummary(errors);
    }

    internal static string BuildUpdateFailureMessage(LocalModlistUpdateResult updateResult)
    {
        var errorMessage =
            updateResult.ErrorMessage ??
            "The modlist could not be updated.";

        return updateResult.FailureStage == LocalModlistUpdateFailureStage.Read
            ? $"Failed to read the modlist:\n{errorMessage}"
            : updateResult.FailureStage == LocalModlistUpdateFailureStage.Write
                ? $"Failed to update the modlist:\n{errorMessage}"
                : errorMessage;
    }

    private static string BuildErrorSummary(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return string.Join(
            "\n",
            errors.Select(error => $"\u2022 {error}"));
    }
}
