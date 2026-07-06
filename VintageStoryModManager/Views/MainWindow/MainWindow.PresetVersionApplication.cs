#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                (!string.IsNullOrWhiteSpace(desiredVersion)
                 && string.Equals(opt.Version, desiredVersion, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(desiredNormalized)
                    && !string.IsNullOrWhiteSpace(opt.NormalizedVersion)
                    && string.Equals(opt.NormalizedVersion, desiredNormalized, StringComparison.OrdinalIgnoreCase)));

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
            var builder = new StringBuilder();

            if (missingMods.Count > 0)
            {
                builder.AppendLine("The following mods from the preset could not be installed:");
                foreach (var modId in missingMods.Distinct(StringComparer.OrdinalIgnoreCase))
                    builder.AppendLine($" • {modId}");
            }

            if (missingVersions.Count > 0)
            {
                if (builder.Length > 0) builder.AppendLine();

                builder.AppendLine("The following mod versions could not be located:");
                foreach (var entry in missingVersions.Distinct(StringComparer.OrdinalIgnoreCase))
                    builder.AppendLine($" • {entry}");
            }

            if (installFailures.Count > 0)
            {
                if (builder.Length > 0) builder.AppendLine();

                builder.AppendLine("Some mods failed to install:");
                foreach (var failure in installFailures.Distinct(StringComparer.OrdinalIgnoreCase))
                    builder.AppendLine($" • {failure}");
            }

            if (missingMods.Count > 0
                && !string.IsNullOrWhiteSpace(_recentLocalModBackupDirectory)
                && _recentLocalModBackupModNames is { Count: > 0 })
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.AppendLine("Local copies of mods that are not on the mod database were saved to:");
                builder.AppendLine($" • {_recentLocalModBackupDirectory}");

                var distinctBackups = _recentLocalModBackupModNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctBackups.Count > 0)
                {
                    builder.AppendLine("Backed up mods:");
                    foreach (var backupName in distinctBackups) builder.AppendLine($"   • {backupName}");
                }
            }

            var message = builder.ToString().Trim();
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
            (!string.IsNullOrWhiteSpace(r.Version)
             && string.Equals(r.Version.Trim(), desiredVersion, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(desiredNormalized)
                && !string.IsNullOrWhiteSpace(r.NormalizedVersion)
                && string.Equals(r.NormalizedVersion, desiredNormalized, StringComparison.OrdinalIgnoreCase)));

        if (release is null)
            return new PresetModInstallResult(false, false, true,
                "The specified version could not be found on the mod database.");

        if (!TryGetDependencyInstallTargetPath(modId, release, out var targetPath, out var pathError))
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
