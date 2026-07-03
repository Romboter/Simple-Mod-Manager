#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

using ComboBox = System.Windows.Controls.ComboBox;
using Popup = System.Windows.Controls.Primitives.Popup;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void UpdateModButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isModUpdateInProgress) return;

        if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

        e.Handled = true;

        IReadOnlyDictionary<ModListItemViewModel, ModReleaseInfo>? overrides = null;
        if (mod.SelectedVersionOption is { Release: { } selectedRelease, IsInstalled: false })
            overrides = new Dictionary<ModListItemViewModel, ModReleaseInfo>
            {
                [mod] = selectedRelease
            };

        await UpdateModsAsync(new[] { mod }, false, overrides);
    }

    private async void SelectedModVersionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isModUpdateInProgress) return;

        if (sender is not ComboBox comboBox) return;

        if (!comboBox.IsDropDownOpen && !comboBox.IsKeyboardFocusWithin) return;

        if (_viewModel?.SelectedMod is not ModListItemViewModel mod) return;

        if (comboBox.SelectedItem is not ModVersionOptionViewModel option) return;

        if (option.IsInstalled || option.Release is null) return;

        if (string.Equals(mod.Version, option.Version, StringComparison.OrdinalIgnoreCase)) return;

        var overrides = new Dictionary<ModListItemViewModel, ModReleaseInfo>
        {
            [mod] = option.Release
        };

        // Use isBulk=true to leverage the modlist install UI overlay for smooth progress feedback
        // and to prevent UI freezing. Pass showSummary=false to skip the bulk changelog dialog.
        await UpdateModsAsync(new[] { mod }, isBulk: true, overrides, showSummary: false);
    }

    private void SelectedModVersionComboBox_OnDropDownOpened(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox) return;

        void ScrollToTop()
        {
            ScrollViewer? scrollViewer = null;

            if (comboBox.Template?.FindName("Popup", comboBox) is Popup popup)
                scrollViewer = FindDescendantScrollViewer(popup.Child);

            scrollViewer ??= FindDescendantScrollViewer(comboBox);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToHome();
                scrollViewer.ScrollToVerticalOffset(0);
            }
        }

        comboBox.Dispatcher.BeginInvoke((Action)ScrollToTop, DispatcherPriority.Background);
    }

    private async void UpdateAllModsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreset) return;

        if (_isModUpdateInProgress || _viewModel?.ModsView == null) return;

        var mods = _viewModel.ModsView.Cast<ModListItemViewModel>()
            .Where(mod => mod.CanUpdate)
            .ToList();

        Dictionary<ModListItemViewModel, ModReleaseInfo>? overrides = null;
        foreach (var mod in mods)
            if (mod.SelectedVersionOption is { Release: { } selectedRelease, IsInstalled: false })
            {
                overrides ??= new Dictionary<ModListItemViewModel, ModReleaseInfo>();
                overrides[mod] = selectedRelease;
            }

        if (mods.Count == 0)
        {
            WpfMessageBox.Show("All mods are already up to date.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new UpdateModsDialog(_userConfiguration, mods, overrides)
        {
            Owner = this
        };

        var dialogResult = dialog.ShowDialog();
        if (dialogResult != true) return;

        var selectedMods = dialog.SelectedMods;
        if (selectedMods.Count == 0) return;

        Dictionary<ModListItemViewModel, ModReleaseInfo>? selectedOverrides = null;
        if (overrides != null)
            foreach (var mod in selectedMods)
                if (overrides.TryGetValue(mod, out var release) && release != null)
                {
                    selectedOverrides ??= new Dictionary<ModListItemViewModel, ModReleaseInfo>();
                    selectedOverrides[mod] = release;
                }

        await CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait(true);
        await UpdateModsAsync(selectedMods, true, selectedOverrides).ConfigureAwait(true);
    }

    private async Task UpdateModsAsync(
        IReadOnlyList<ModListItemViewModel> mods,
        bool isBulk,
        IReadOnlyDictionary<ModListItemViewModel, ModReleaseInfo>? releaseOverrides = null,
        bool showSummary = true,
        Func<ModListItemViewModel, ModReleaseInfo, IProgress<ModUpdateProgress>?>? progressFactory = null,
        Action<ModListItemViewModel, ModReleaseInfo, ModUpdateResult>? completionCallback = null)
    {
        if (_viewModel is null || mods.Count == 0)
        {
            await RefreshDeleteCachedModsMenuHeaderAsync();
            return;
        }

        _isModUpdateInProgress = true;
        UpdateSelectedModButtons();

        // Use the modlist install overlay UI for bulk updates when no custom progress factory is provided
        var useModlistInstallUi = isBulk && progressFactory is null;
        if (useModlistInstallUi)
            BeginModlistInstallUi(mods.Count, "Updating mods...");

        try
        {
            var results = new List<ModUpdateOperationResult>();
            ModUpdateReleasePreference? bulkPreference = null;
            var abortRequested = false;
            var requiresRefresh = false;

            foreach (var mod in mods)
            {
                var displayName = mod.DisplayName ?? mod.ModId ?? "Mod";
                ModReleaseInfo? overrideRelease = null;
                var hasOverride = releaseOverrides != null
                                  && releaseOverrides.TryGetValue(mod, out overrideRelease);

                if (!hasOverride && !mod.CanUpdate) continue;

                if (!TryGetManagedModPath(mod, out var modPath, out var pathError))
                {
                    var message = string.IsNullOrWhiteSpace(pathError)
                        ? "The mod location could not be determined."
                        : pathError!;
                    results.Add(ModUpdateOperationResult.Failure(mod, message));
                    requiresRefresh = true;
                    if (completionCallback != null && hasOverride && overrideRelease != null)
                        completionCallback(mod, overrideRelease, new ModUpdateResult(false, message));
                    if (useModlistInstallUi)
                        CompleteModlistInstallStep($"{displayName}: {message}");
                    continue;
                }

                var previousResultCount = results.Count;
                var release = hasOverride
                    ? overrideRelease
                    : SelectReleaseForMod(mod, isBulk, ref bulkPreference, results, ref abortRequested);
                if (abortRequested)
                {
                    requiresRefresh = true;
                    break;
                }

                if (release is null)
                {
                    if (results.Count > previousResultCount) requiresRefresh = true;
                    continue;
                }

                var targetIsDirectory = Directory.Exists(modPath);

                if (!targetIsDirectory && !File.Exists(modPath) && mod.SourceKind == ModSourceKind.Folder)
                    targetIsDirectory = true;

                var targetPath = modPath;
                string? existingPath = null;

                if (!targetIsDirectory)
                {
                    if (!TryGetUpdateTargetPath(mod, release, modPath, out var resolvedPath, out var targetError))
                    {
                        var failureMessage = string.IsNullOrWhiteSpace(targetError)
                            ? "The mod location could not be determined."
                            : targetError!;
                        results.Add(ModUpdateOperationResult.Failure(mod, failureMessage));
                        requiresRefresh = true;
                        completionCallback?.Invoke(mod, release, new ModUpdateResult(false, failureMessage));
                        if (useModlistInstallUi)
                            CompleteModlistInstallStep($"{displayName}: {failureMessage}");
                        continue;
                    }

                    targetPath = resolvedPath;
                    existingPath = modPath;
                }

                var descriptor = new ModUpdateDescriptor(
                    mod.ModId ?? displayName,
                    displayName,
                    release.DownloadUri,
                    targetPath,
                    targetIsDirectory,
                    release.FileName,
                    release.Version,
                    mod.Version)
                {
                    ExistingPath = existingPath
                };

                IProgress<ModUpdateProgress>? progress;
                if (useModlistInstallUi)
                {
                    progress = CreateModlistInstallProgressReporter(displayName);
                }
                else
                {
                    var additionalProgress = progressFactory?.Invoke(mod, release);
                    progress = CreateModUpdateProgressReporter(displayName, additionalProgress);
                }

                var updateResult = await _modUpdateService
                    .UpdateAsync(descriptor, _userConfiguration.CacheAllVersionsLocally, progress)
                    .ConfigureAwait(true);

                completionCallback?.Invoke(mod, release, updateResult);

                if (!updateResult.Success)
                {
                    var failureMessage = string.IsNullOrWhiteSpace(updateResult.ErrorMessage)
                        ? "The update failed."
                        : updateResult.ErrorMessage!;
                    if (useModlistInstallUi)
                        CompleteModlistInstallStep($"{displayName}: {failureMessage}");
                    else
                        _viewModel.ReportStatus($"Failed to update {displayName}: {failureMessage}", true);
                    results.Add(ModUpdateOperationResult.Failure(mod, failureMessage));
                    requiresRefresh = true;
                    continue;
                }

                requiresRefresh = true;
                if (useModlistInstallUi)
                {
                    CompleteModlistInstallStep($"Updated {displayName} to {release.Version}.");
                    _modActivityLoggingService.LogModUpdate(mod.DisplayName ?? mod.ModId ?? "Unknown", mod.Version, release.Version);
                }
                else
                {
                    _viewModel.ReportStatus($"Updated {displayName} to {release.Version}.");
                    _modActivityLoggingService.LogModUpdate(mod.DisplayName ?? mod.ModId ?? "Unknown", mod.Version, release.Version);
                }
                await _viewModel.PreserveActivationStateAsync(mod.ModId ?? string.Empty, mod.Version, release.Version, mod.IsActive)
                    .ConfigureAwait(true);
                var appliedChangelogEntries =
                    mod.GetChangelogEntriesForUpgrade(release.Version);
                var changelogSummary = ModChangelogFormatter.BuildChangelogSummary(appliedChangelogEntries);
                results.Add(
                    ModUpdateOperationResult.SuccessResult(mod, release.Version, mod.Version, changelogSummary));
            }

            if (requiresRefresh && _viewModel.RefreshCommand != null)
                try
                {
                    await RefreshModsAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    WpfMessageBox.Show(
                        $"The mod list could not be refreshed after updating mods:{Environment.NewLine}{ex.Message}",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

            if (abortRequested && !useModlistInstallUi)
                _viewModel.ReportStatus(isBulk ? "Bulk update cancelled." : "Update cancelled.");

            if (results.Count > 0 && showSummary)
            {
                if (isBulk) ShowBulkUpdateChangelogDialog(results);

                ShowUpdateSummary(results, isBulk, abortRequested);
            }
            else if (abortRequested && showSummary)
            {
                WpfMessageBox.Show(isBulk ? "Bulk update cancelled." : "Update cancelled.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        finally
        {
            if (useModlistInstallUi)
                EndModlistInstallUi();
            _isModUpdateInProgress = false;
            UpdateSelectedModButtons();
        }

        await RefreshDeleteCachedModsMenuHeaderAsync();
    }

    private ModReleaseInfo? SelectReleaseForMod(
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

    private void ShowBulkUpdateChangelogDialog(IReadOnlyList<ModUpdateOperationResult> results)
    {
        if (results is not { Count: > 0 }) return;

        var items = new List<BulkUpdateChangelogWindow.BulkUpdateChangelogItem>();

        foreach (var result in results)
        {
            if (!result.Success) continue;

            var fromVersion = string.IsNullOrWhiteSpace(result.OldVersion)
                ? "Unknown"
                : result.OldVersion!;
            var toVersion = string.IsNullOrWhiteSpace(result.NewVersion)
                ? "Unknown"
                : result.NewVersion!;
            var title = $"{result.Mod.DisplayName} ({fromVersion} → {toVersion})";
            var changelog = string.IsNullOrWhiteSpace(result.ChangelogSummary)
                ? "No changelog entries were provided for this update."
                : result.ChangelogSummary!;
            items.Add(new BulkUpdateChangelogWindow.BulkUpdateChangelogItem(title, changelog));
        }

        if (items.Count == 0) return;

        var dialog = new BulkUpdateChangelogWindow(items)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private static void ShowUpdateSummary(IReadOnlyList<ModUpdateOperationResult> results, bool isBulk, bool aborted)
    {
        if (results.Count == 0) return;

        var successCount = results.Count(result => result.Success);
        var failureCount = results.Count(result => !result.Success && !result.Skipped);
        var skippedCount = results.Count(result => result.Skipped);

        if (!isBulk && failureCount == 0 && skippedCount == 0) return;

        if (isBulk && failureCount == 0 && skippedCount == 0 && !aborted) return;

        var builder = new StringBuilder();
        builder.AppendLine(isBulk ? "Bulk update completed." : "Update completed.");
        if (aborted) builder.AppendLine("The operation was cancelled.");

        builder.AppendLine($"Updated: {successCount}");

        if (failureCount > 0) builder.AppendLine($"Failed: {failureCount}");

        if (skippedCount > 0) builder.AppendLine($"Skipped: {skippedCount}");

        if (failureCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Failures:");
            foreach (var failure in results.Where(result => !result.Success && !result.Skipped))
                builder.AppendLine($" • {failure.Mod.DisplayName}: {failure.Message}");
        }

        if (skippedCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Skipped:");
            foreach (var skipped in results.Where(result => result.Skipped))
                builder.AppendLine($" • {skipped.Mod.DisplayName}: {skipped.Message}");
        }

        MessageBoxImage icon;
        if (isBulk)
            icon = MessageBoxImage.None;
        else
            icon = failureCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
        WpfMessageBox.Show(builder.ToString(), "Simple VS Manager", MessageBoxButton.OK, icon);
    }
}
