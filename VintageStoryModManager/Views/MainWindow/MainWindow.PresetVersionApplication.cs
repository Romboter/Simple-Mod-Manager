#nullable enable

using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task<bool> ApplyPresetModVersionsAsync(ModPreset preset)
    {
        if (_viewModel?.ModsView is null) return false;

        var mods = _viewModel.ModsView.Cast<ModListItemViewModel>().ToList();
        var modLookup = mods.ToDictionary(mod => mod.ModId, StringComparer.OrdinalIgnoreCase);
        var overrides = new Dictionary<ModListItemViewModel, ModReleaseInfo>();
        var missingVersions = new List<string>();
        var missingMods = new List<string>();
        var installFailures = new List<string>();
        var installCandidates = new List<ModPresetModState>();

        foreach (var state in preset.ModStates)
        {
            if (!modLookup.TryGetValue(state.ModId, out var mod))
            {
                installCandidates.Add(state);
                continue;
            }

            var desiredVersion = string.IsNullOrWhiteSpace(state.Version) ? null : state.Version!.Trim();
            if (string.IsNullOrWhiteSpace(desiredVersion)) continue;

            var installedVersion = string.IsNullOrWhiteSpace(mod.Version) ? null : mod.Version!.Trim();

            if (VersionStringUtility.VersionsMatch(desiredVersion, installedVersion)) continue;

            var desiredNormalized = VersionStringUtility.Normalize(desiredVersion);

            var option = mod.VersionOptions.FirstOrDefault(opt =>
                VersionStringUtility.MatchesDesiredVersion(desiredVersion, desiredNormalized, opt.Version, opt.NormalizedVersion));

            if (option is null)
            {
                missingVersions.Add($"{mod.DisplayName} ({desiredVersion})");
                continue;
            }

            if (option.IsInstalled) continue;

            if (!option.HasRelease || option.Release is null)
            {
                var display = !string.IsNullOrWhiteSpace(option.Version)
                    ? option.Version
                    : desiredVersion ?? "Unknown";
                missingVersions.Add($"{mod.DisplayName} ({display})");
                continue;
            }

            overrides[mod] = option.Release;
        }

        var totalInstallOperations = installCandidates.Count + overrides.Count;
        var showInstallOverlay = totalInstallOperations > 0;
        var hasOverrides = overrides.Count > 0;

        if (showInstallOverlay)
            BeginModlistInstallUi(totalInstallOperations, "Installing mods from the modlist...");

        try
        {
            if (installCandidates.Count > 0)
            {
                foreach (var candidate in installCandidates)
                {
                    var progress = showInstallOverlay
                        ? CreateModlistInstallProgressReporter(candidate.ModId)
                        : null;

                    var installResult = await TryInstallPresetModAsync(candidate, progress).ConfigureAwait(true);
                    if (installResult.Success)
                    {
                        if (showInstallOverlay)
                        {
                            var display = string.IsNullOrWhiteSpace(candidate.ModId) ? "mod" : candidate.ModId!;
                            CompleteModlistInstallStep($"Installed {display}.");
                        }

                        continue;
                    }

                    var desiredVersion = string.IsNullOrWhiteSpace(candidate.Version)
                        ? "Unknown"
                        : candidate.Version!.Trim();

                    if (installResult.ModMissing)
                    {
                        var modDisplay = string.IsNullOrWhiteSpace(candidate.ModId)
                            ? "<unknown mod>"
                            : candidate.ModId!;
                        var display = string.IsNullOrWhiteSpace(installResult.ErrorMessage)
                            ? modDisplay
                            : $"{modDisplay} — {installResult.ErrorMessage}";
                        missingMods.Add(display);

                        if (showInstallOverlay)
                            CompleteModlistInstallStep($"{modDisplay} was not found.");

                        continue;
                    }

                    if (installResult.VersionMissing)
                    {
                        var modDisplay = string.IsNullOrWhiteSpace(candidate.ModId)
                            ? "<unknown mod>"
                            : candidate.ModId!;
                        missingVersions.Add($"{modDisplay} ({desiredVersion})");

                        if (showInstallOverlay)
                            CompleteModlistInstallStep($"{modDisplay} {desiredVersion} unavailable.");

                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(installResult.ErrorMessage))
                        installFailures.Add($"{candidate.ModId}: {installResult.ErrorMessage}");

                    if (showInstallOverlay)
                    {
                        var displayName = string.IsNullOrWhiteSpace(candidate.ModId) ? "Mod" : candidate.ModId!;
                        var failureMessage = string.IsNullOrWhiteSpace(installResult.ErrorMessage)
                            ? "Installation failed."
                            : installResult.ErrorMessage!;
                        CompleteModlistInstallStep($"{displayName}: {failureMessage}");
                    }
                }

            }

            if (hasOverrides)
            {
                Func<ModListItemViewModel, ModReleaseInfo, IProgress<ModUpdateProgress>?>? progressFactory = null;
                Action<ModListItemViewModel, ModReleaseInfo, ModUpdateResult>? completionCallback = null;

                if (showInstallOverlay)
                {
                    progressFactory = (mod, _) => CreateModlistInstallProgressReporter(mod.DisplayName ?? mod.ModId);
                    completionCallback = (mod, release, result) =>
                    {
                        var displayName = string.IsNullOrWhiteSpace(mod.DisplayName)
                            ? mod.ModId ?? "Mod"
                            : mod.DisplayName!;
                        var versionSuffix = string.IsNullOrWhiteSpace(release.Version)
                            ? string.Empty
                            : $" {release.Version}";
                        var message = result.Success
                            ? $"Updated {displayName}{versionSuffix}."
                            : $"{displayName}: {(!string.IsNullOrWhiteSpace(result.ErrorMessage)
                                ? result.ErrorMessage
                                : "Update failed.")}";

                        CompleteModlistInstallStep(message);
                    };
                }

                await UpdateModsAsync(overrides.Keys.ToList(), true, overrides, false, progressFactory,
                        completionCallback)
                    .ConfigureAwait(true);
            }
        }
        finally
        {
            if (showInstallOverlay) EndModlistInstallUi();
        }

        if (missingMods.Count > 0 || missingVersions.Count > 0 || installFailures.Count > 0)
        {
            var message = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
                missingMods,
                missingVersions,
                installFailures,
                _recentLocalModBackupDirectory,
                _recentLocalModBackupModNames);

            if (!string.IsNullOrWhiteSpace(message))
                WpfMessageBox.Show(message,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        }

        return showInstallOverlay;
    }

    private async Task<PresetModInstallResult> TryInstallPresetModAsync(ModPresetModState state,
        IProgress<ModUpdateProgress>? installProgress = null)
    {
        if (_viewModel is null)
            return new PresetModInstallResult(false, false, false, "The mod view model is not available.");

        var modId = state.ModId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modId))
            return new PresetModInstallResult(false, false, false, "The preset entry is missing a mod identifier.");

        var desiredVersion = string.IsNullOrWhiteSpace(state.Version) ? null : state.Version!.Trim();
        if (string.IsNullOrWhiteSpace(desiredVersion))
            return new PresetModInstallResult(false, false, true, "No version was recorded for this mod.");

        var info = await _modDatabaseService
            .TryLoadDatabaseInfoAsync(modId, desiredVersion, _viewModel.InstalledGameVersion,
                _userConfiguration.RequireExactVsVersionMatch)
            .ConfigureAwait(true);

        if (info is null) return new PresetModInstallResult(false, true, false, "Mod not found on the mod database.");

        var desiredNormalized = VersionStringUtility.Normalize(desiredVersion);
        var releases = info.Releases ?? Array.Empty<ModReleaseInfo>();
        var release = releases.FirstOrDefault(r =>
            VersionStringUtility.MatchesDesiredVersion(desiredVersion, desiredNormalized, r.Version?.Trim(), r.NormalizedVersion));

        if (release is null)
            return new PresetModInstallResult(false, false, true,
                "The specified version could not be found on the mod database.");

        if (!ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(_dataDirectory, modId, release,
                out var targetPath, out var pathError))
            return new PresetModInstallResult(false, false, false, pathError);

        var descriptor = new ModUpdateDescriptor(
            modId,
            modId,
            release.DownloadUri,
            targetPath,
            false,
            release.FileName,
            release.Version,
            null);

        var progress = CreateModUpdateProgressReporter(modId, installProgress);

        var outcome = await ModUpdateOperationHelper.ExecuteAsync(
                _modUpdateService, descriptor, _userConfiguration.CacheAllVersionsLocally, progress,
                "The installation failed.")
            .ConfigureAwait(true);

        if (!outcome.Success)
            return new PresetModInstallResult(false, false, false, outcome.ErrorMessage);

        var versionSuffix = string.IsNullOrWhiteSpace(release.Version) ? string.Empty : $" {release.Version}";
        _viewModel.ReportStatus($"Installed {modId}{versionSuffix}.");
        _modActivityLoggingService.LogModInstall(modId, release.Version);

        return new PresetModInstallResult(true, false, false, null);
    }
}
