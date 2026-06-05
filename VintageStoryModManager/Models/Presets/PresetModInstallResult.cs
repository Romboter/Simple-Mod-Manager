namespace VintageStoryModManager;

internal readonly record struct PresetModInstallResult(
    bool Success,
    bool ModMissing,
    bool VersionMissing,
    string? ErrorMessage);
