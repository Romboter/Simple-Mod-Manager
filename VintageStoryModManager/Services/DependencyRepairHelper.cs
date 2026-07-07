#nullable enable

using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

/// <summary>
///     Determines whether a mod dependency needs to be installed or updated.
/// </summary>
internal static class DependencyRepairHelper
{
    internal static bool IsDependencyMissing(
        bool listedAsMissing,
        ModListItemViewModel? installedDependency,
        string? requiredVersion)
    {
        if (listedAsMissing) return true;

        if (installedDependency is null) return true;

        return !VersionStringUtility.SatisfiesMinimumVersion(requiredVersion, installedDependency.Version);
    }
}
