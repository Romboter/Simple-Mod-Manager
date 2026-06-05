namespace VintageStoryModManager;

internal readonly record struct PresetLoadOptions(
    bool ApplyModStatus,
    bool ApplyModVersions,
    bool ForceExclusive);
