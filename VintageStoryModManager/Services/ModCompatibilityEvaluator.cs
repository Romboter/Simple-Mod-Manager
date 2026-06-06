using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal static class ModCompatibilityEvaluator
{
    internal static CompatibilityEvaluation EvaluateCompatibility(
            ModListItemViewModel mod,
            string targetVersion,
            string displayName,
            bool requireExactMatch)
        {
            var installedVersion = string.IsNullOrWhiteSpace(mod.Version)
                ? "Unknown"
                : mod.Version!;

            var installedOption = mod.VersionOptions
                .FirstOrDefault(option => option is { IsInstalled: true });
            var installedRelease = installedOption?.Release;

            var releases = mod.VersionOptions
                .Select(option => option.Release)
                .Where(release => release != null)
                .GroupBy(release => release!.Version, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()!)
                .ToList();

            var compatibleRelease = releases
                .FirstOrDefault(release => ReleaseSupportsVersion(release, targetVersion, requireExactMatch));

            if (installedRelease != null)
            {
                if (ReleaseSupportsVersion(installedRelease, targetVersion, requireExactMatch))
                    return CompatibilityEvaluation.Compatible;

                if (compatibleRelease != null
                    && !string.Equals(compatibleRelease.Version, installedRelease.Version,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var message =
                        $"{displayName}: Installed version {installedRelease.Version} is not marked as compatible with Vintage Story {targetVersion}. Update to version {compatibleRelease.Version} or later.";
                    return CompatibilityEvaluation.Incompatible(message);
                }

                if (installedRelease.GameVersionTags is { Count: > 0 })
                {
                    var message =
                        $"{displayName}: Installed version {installedVersion} is not marked as compatible with Vintage Story {targetVersion}. No compatible update was found.";
                    return CompatibilityEvaluation.Incompatible(message);
                }
            }

            if (compatibleRelease != null)
            {
                var message =
                    $"{displayName}: Installed version {installedVersion} is not marked as compatible with Vintage Story {targetVersion}. Update to version {compatibleRelease.Version} or later.";
                return CompatibilityEvaluation.Incompatible(message);
            }

            var dependencies = mod.Dependencies ?? Array.Empty<ModDependencyInfo>();
            var hasGameDependency = false;

            foreach (var dependency in dependencies)
            {
                if (dependency is null || !dependency.IsGameOrCoreDependency ||
                    string.IsNullOrWhiteSpace(dependency.Version)) continue;

                hasGameDependency = true;
                if (!VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, targetVersion))
                {
                    var message =
                        $"{displayName}: Requires Vintage Story {dependency.Version} or newer.";
                    return CompatibilityEvaluation.Incompatible(message);
                }
            }

            if (hasGameDependency) return CompatibilityEvaluation.Compatible;

            var unknownMessage =
                $"{displayName}: No compatibility metadata is available for Vintage Story {targetVersion}.";
            return CompatibilityEvaluation.Unknown(unknownMessage);
        }

    private static bool ReleaseSupportsVersion(ModReleaseInfo release, string targetVersion, bool requireExactMatch)
        {
            if (release.GameVersionTags is not { Count: > 0 }) return false;

            foreach (var tag in release.GameVersionTags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;

                if (VersionStringUtility.SupportsVersion(tag, targetVersion, requireExactMatch)) return true;
            }

            return false;
        }
}
