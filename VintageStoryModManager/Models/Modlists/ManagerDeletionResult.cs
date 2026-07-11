namespace VintageStoryModManager;

internal readonly record struct ManagerDeletionResult(
    List<string> DeletedPaths,
    List<string> FailedPaths);
