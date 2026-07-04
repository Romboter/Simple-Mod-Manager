#nullable enable

using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal static class ModReleaseSelectionHelper
{
    internal static ModReleaseInfo? SelectReleaseForMod(
        ModListItemViewModel mod,
        bool isBulk,
        ref ModUpdateReleasePreference? bulkPreference,
        List<ModUpdateOperationResult> results,
        ref bool abortRequested)
    {
        var latest = mod.LatestRelease;
        if (latest is null)
        {
            results.Add(ModUpdateOperationResult.SkippedResult(mod, "No downloadable release was found."));
            return null;
        }

        if (bulkPreference.HasValue && bulkPreference.Value == ModUpdateReleasePreference.LatestCompatible)
            if (mod.LatestCompatibleRelease != null)
                return mod.LatestCompatibleRelease;

        // No compatible release is available; fall back to installing the latest release.
        return latest;
    }

    internal static ModReleaseInfo? SelectReleaseForInstall(ModListItemViewModel mod)
    {
        if (mod.LatestRelease?.IsCompatibleWithInstalledGame == true) return mod.LatestRelease;

        if (mod.LatestCompatibleRelease != null) return mod.LatestCompatibleRelease;

        return mod.LatestRelease;
    }
}
