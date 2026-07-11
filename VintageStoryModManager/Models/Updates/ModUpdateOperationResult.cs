using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager;

internal readonly record struct ModUpdateOperationResult(
    ModListItemViewModel Mod,
    bool Success,
    bool Skipped,
    string Message,
    string? OldVersion,
    string? NewVersion,
    string? ChangelogSummary)
{
    public static ModUpdateOperationResult SuccessResult(
        ModListItemViewModel mod,
        string newVersion,
        string? previousVersion,
        string? changelogSummary)
    {
        return new ModUpdateOperationResult(
            mod,
            true,
            false,
            $"Updated to {newVersion}.",
            previousVersion,
            newVersion,
            changelogSummary);
    }

    public static ModUpdateOperationResult Failure(
        ModListItemViewModel mod,
        string message)
    {
        return new ModUpdateOperationResult(
            mod,
            false,
            false,
            message,
            mod.Version,
            null,
            null);
    }

    public static ModUpdateOperationResult SkippedResult(
        ModListItemViewModel mod,
        string message)
    {
        return new ModUpdateOperationResult(
            mod,
            false,
            true,
            message,
            mod.Version,
            null,
            null);
    }
}
