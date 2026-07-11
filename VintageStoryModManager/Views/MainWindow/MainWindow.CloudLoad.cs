#nullable enable

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void RefreshCloudModlistsButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshCloudModlistsAsync(true);
    }

    private void CloudModlistsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CloudModlistsDataGrid?.SelectedItem is CloudModlistListEntry entry)
            SetCloudModlistSelection(entry);
        else
            SetCloudModlistSelection(null);
    }

    private async void InstallCloudModlistButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _selectedCloudModlist is not CloudModlistListEntry entry) return;

        var ensuredEntry = await EnsureCloudModlistContentAsync(entry);
        if (ensuredEntry is null) return;
        entry = ensuredEntry;

        string cacheDirectory;
        try
        {
            cacheDirectory =
                EnsureCloudModListCacheDirectory();
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            await _confirmationService.NotifyAsync(
                    CloudLoadDialogTextBuilder.BuildCachePreparationFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        string cacheFilePath;
        try
        {
            cacheFilePath =
                await CloudModlistCacheService.CacheAsync(
                    cacheDirectory,
                    entry);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            await _confirmationService.NotifyAsync(
                    CloudLoadDialogTextBuilder.BuildSelectedModlistCacheFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        var loadMode = await PromptModlistLoadModeAsync().ConfigureAwait(true);
        if (loadMode is not ModlistLoadMode mode) return;

        if (mode == ModlistLoadMode.Replace && !await EnsureModlistBackupBeforeLoadAsync().ConfigureAwait(true)) return;

        PrepareForModlistLoad();

        var loadOptions = GetModlistLoadOptions(mode);
        var fallbackName = entry.Name ?? entry.DisplayName ?? "Modlist";

        if (!PresetFileLoader.TryLoadPresetFromFile(cacheFilePath,
                fallbackName,
                loadOptions,
                out var preset,
                out var errorMessage))
        {
            var message = CloudLoadDialogTextBuilder.BuildDownloadedModlistLoadFailureMessage(errorMessage);
            await _confirmationService.NotifyAsync(
                    message,
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        if (preset is null) return;

        await CreateAutomaticBackupAsync("ModlistLoaded").ConfigureAwait(true);
        await ApplyPresetAsync(preset);
        var status = mode == ModlistLoadMode.Replace
            ? $"Installed cloud modlist \"{preset.Name}\"."
            : $"Added mods from cloud modlist \"{preset.Name}\".";
        _viewModel.ReportStatus(status);
    }

    private async void LoadModlistFromCloudMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCloudOperationAsync(async store =>
        {
            var slots =
                await CloudModlistSlotService.LoadSlotsAsync(
                    store,
                    false,
                    true);
            if (slots.Count == 0)
            {
                await _confirmationService.NotifyAsync(
                        CloudLoadDialogTextBuilder.NoCloudModlistsAvailableMessage,
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);
                return;
            }

            var dialog = new CloudSlotSelectionDialog(this,
                slots,
                "Load Cloud Modlist",
                "Select a cloud modlist to load.");
            var dialogResult = dialog.ShowDialog();
            if (dialogResult != true || dialog.SelectedSlot is not CloudModlistSlot selectedSlot) return;

            var loadMode = await PromptModlistLoadModeAsync().ConfigureAwait(true);
            if (loadMode is not ModlistLoadMode mode) return;

            if (mode == ModlistLoadMode.Replace && !await EnsureModlistBackupBeforeLoadAsync().ConfigureAwait(true)) return;

            PrepareForModlistLoad();

            var loadOptions = GetModlistLoadOptions(mode);
            var json =
                await CloudModlistContentService.EnsureSlotContentAsync(
                    store,
                    selectedSlot);

            if (string.IsNullOrWhiteSpace(json))
            {
                await _confirmationService.NotifyAsync(
                        CloudLoadDialogTextBuilder.SelectedCloudModlistEmptyMessage,
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                return;
            }

            var sourceName = selectedSlot.Name ?? CloudModlistHelper.FormatCloudSlotLabel(selectedSlot.SlotKey);
            if (!PresetFileLoader.TryLoadPresetFromJson(json,
                    "Modlist",
                    loadOptions,
                    out var preset,
                    out var errorMessage,
                    sourceName))
            {
                var message = CloudLoadDialogTextBuilder.BuildCloudModlistLoadFailureMessage(errorMessage);
                await _confirmationService.NotifyAsync(
                        message,
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
                return;
            }

            var loadedModlist = preset!;
            await CreateAutomaticBackupAsync("ModlistLoaded").ConfigureAwait(true);
            await ApplyPresetAsync(loadedModlist);
            var slotLabel = CloudModlistHelper.FormatCloudSlotLabel(selectedSlot.SlotKey);
            var status = mode == ModlistLoadMode.Replace
                ? $"Loaded cloud modlist \"{loadedModlist.Name}\" from {slotLabel}."
                : $"Added mods from cloud modlist \"{loadedModlist.Name}\" from {slotLabel}.";
            _viewModel?.ReportStatus(status);
        }, "load the modlist from the cloud");
    }

    private async void DeleteCloudModlistMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCloudOperationAsync(async store =>
        {
            var slots =
                await CloudModlistSlotService.LoadSlotsAsync(
                    store,
                    false,
                    false);
            if (slots.Count == 0)
            {
                await _confirmationService.NotifyAsync(
                        CloudLoadDialogTextBuilder.NoCloudModlistsAvailableToDeleteMessage,
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);
                return;
            }

            var dialog = new CloudSlotSelectionDialog(this,
                slots,
                "Delete Cloud Modlist",
                "Select the cloud modlist you want to delete.");
            var dialogResult = dialog.ShowDialog();
            if (dialogResult != true || dialog.SelectedSlot is not CloudModlistSlot selectedSlot) return;

            var slotLabel = CloudModlistHelper.FormatCloudSlotLabel(selectedSlot.SlotKey);
            var displayName = string.IsNullOrWhiteSpace(selectedSlot.Name)
                ? slotLabel
                : $"{slotLabel} (\"{selectedSlot.Name}\")";

            var confirmed = await _confirmationService.ConfirmAsync(
                    CloudLoadDialogTextBuilder.BuildDeleteConfirmationMessage(displayName),
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);

            if (!confirmed) return;

            await CloudModlistManagementService.DeleteSlotAsync(
                store,
                selectedSlot);

            _viewModel?.ReportStatus(
                $"Deleted cloud modlist from {slotLabel}.");
        }, "delete the cloud modlist");

        if (_viewModel?.IsViewingModlistTab == true)
            await RefreshCloudModlistsAsync(true);
        else
            _cloudModlistsLoaded = false;
    }

    private async Task RefreshCloudModlistsAsync(bool force)
    {
        if (_isCloudModlistRefreshInProgress) return;

        if (!force && _cloudModlistsLoaded) return;

        _isCloudModlistRefreshInProgress = true;
        UpdateCloudModlistControlsEnabledState();

        try
        {
            await ExecuteCloudOperationAsync(async store =>
            {
                var registryEntries = await store.GetRegistryEntriesAsync();
                var listEntries =
                    CloudModlistHelper.BuildListEntries(registryEntries);

                await Dispatcher.InvokeAsync(() =>
                {
                    _viewModel?.ReplaceCloudModlists(listEntries);
                    _cloudModlistsLoaded = true;
                    SetCloudModlistSelection(null);
                    if (CloudModlistsDataGrid != null) CloudModlistsDataGrid.SelectedItem = null;
                }, DispatcherPriority.Background);
            }, "load cloud modlists");
        }
        finally
        {
            _isCloudModlistRefreshInProgress = false;
            UpdateCloudModlistControlsEnabledState();
        }
    }

    private async Task<CloudModlistListEntry?> EnsureCloudModlistContentAsync(CloudModlistListEntry entry)
    {
        if (entry.IsContentComplete && !string.IsNullOrWhiteSpace(entry.ContentJson)) return entry;

        CloudModlistListEntry? refreshedEntry = null;

        await ExecuteCloudOperationAsync(async store =>
        {
            refreshedEntry =
                await CloudModlistContentService.EnsureContentAsync(
                    store,
                    entry);
        }, "download the selected cloud modlist");

        if (refreshedEntry is null)
        {
            await _confirmationService.NotifyAsync(
                    CloudLoadDialogTextBuilder.SelectedCloudModlistDownloadFailedMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return null;
        }

        _viewModel?.TryReplaceCloudModlist(entry, refreshedEntry);
        SetCloudModlistSelection(refreshedEntry);

        return refreshedEntry;
    }

    private void SetCloudModlistSelection(CloudModlistListEntry? entry)
    {
        _selectedCloudModlist = entry;

        if (SelectedModlistTitle is not null) SelectedModlistTitle.Text = entry?.DisplayName ?? string.Empty;

        if (SelectedModlistDescription is not null)
            SelectedModlistDescription.Text = entry?.Description ?? string.Empty;

        UpdateCloudModlistControlsEnabledState();
    }

    private void UpdateCloudModlistControlsEnabledState()
    {
        var internetEnabled = !InternetAccessManager.IsInternetAccessDisabled;

        if (SaveCloudModlistButton is not null) SaveCloudModlistButton.IsEnabled = internetEnabled;

        if (ModifyCloudModlistsButton is not null) ModifyCloudModlistsButton.IsEnabled = internetEnabled;

        if (RefreshCloudModlistsButton is not null)
            RefreshCloudModlistsButton.IsEnabled = internetEnabled && !_isCloudModlistRefreshInProgress;

        if (InstallCloudModlistButton is not null)
        {
            var hasSelection = _selectedCloudModlist is not null;
            InstallCloudModlistButton.IsEnabled = internetEnabled && hasSelection;
        }
    }
}
