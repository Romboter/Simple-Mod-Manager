#nullable enable

using System.Windows;
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

    internal static ModReleaseInfo? SelectReleaseForDependency(ModDependencyInfo dependency, ModDatabaseInfo info)
    {
        if (info is null) return null;

        var releases = info.Releases ?? Array.Empty<ModReleaseInfo>();
        if (releases.Count == 0) return null;

        foreach (var release in releases)
            if (release.IsCompatibleWithInstalledGame
                && VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, release.Version))
                return release;

        foreach (var release in releases)
            if (VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, release.Version))
                return release;

        var fallback = releases.FirstOrDefault(r => r.IsCompatibleWithInstalledGame)
                       ?? releases[0];

        var availableVersion = string.IsNullOrWhiteSpace(fallback.Version)
            ? "the latest available release"
            : $"version {fallback.Version}";

        var requirement = string.IsNullOrWhiteSpace(dependency.Version)
            ? dependency.ModId
            : $"{dependency.ModId} {dependency.Version} or newer";

        var message =
            $"No release that satisfies the required minimum version for {dependency.Display} could be found.{Environment.NewLine}{Environment.NewLine}" +
            $"The mod database only provides {availableVersion}, which may not resolve the dependency requirement for {requirement}.{Environment.NewLine}{Environment.NewLine}" +
            "Do you want to install this older release anyway?";

        var confirmation = ModManagerMessageBox.Show(
            message,
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return confirmation == MessageBoxResult.Yes ? fallback : null;
    }
}
