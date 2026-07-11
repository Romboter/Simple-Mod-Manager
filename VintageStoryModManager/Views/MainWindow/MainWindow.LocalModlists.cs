#nullable enable

using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void LocalModlistsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocalModlistsDataGrid is null)
        {
            SetLocalModlistSelection(Array.Empty<LocalModlistListEntry>());
            return;
        }

        var selectedEntries = LocalModlistsDataGrid.SelectedItems
            .OfType<LocalModlistListEntry>()
            .ToList();

        SetLocalModlistSelection(selectedEntries);
    }

    private async void SaveLocalModlistButton_OnClick(object sender, RoutedEventArgs e)
    {
        var preservedSelection = _selectedLocalModlists
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.FilePath))
            .Select(entry => entry.FilePath)
            .ToList();

        var saveResult = await TrySaveModlistAsync(null).ConfigureAwait(true);
        if (saveResult.Success)
        {
            var savedFilePath = saveResult.SavedFilePath;
            if (!string.IsNullOrWhiteSpace(savedFilePath)) preservedSelection.Add(savedFilePath);
            await RefreshLocalModlistsAsync(true, preservedSelection).ConfigureAwait(true);
        }
    }

    private async void InstallLocalModlistButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedLocalModlists.Count != 1) return;

        var entry = _selectedLocalModlists[0];

        if (string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
        {
            await _confirmationService.NotifyAsync(
                    "The selected modlist file could not be found. It may have been moved or deleted.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            await RefreshLocalModlistsAsync(true).ConfigureAwait(true);
            return;
        }

        await LoadModlistFromFileAsync(entry.FilePath).ConfigureAwait(true);
    }

    private async void OpenModlistsFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        string directory;
        try
        {
            directory = EnsureModListDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            await _confirmationService.NotifyAsync(
                    LocalModlistDialogTextBuilder.BuildFolderFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        FolderOpeningHelper.OpenFolder(directory, "Modlists");
    }

    private async void DeleteLocalModlistsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedLocalModlists.Count == 0) return;

        var entries = _selectedLocalModlists
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.FilePath))
            .ToList();

        if (entries.Count == 0) return;

        var deleteConfirmationMessage = LocalModlistDialogTextBuilder.BuildDeleteConfirmation(entries);
        var confirmed = await _confirmationService.ConfirmAsync(
                deleteConfirmationMessage,
                "Delete Modlists")
            .ConfigureAwait(true);

        if (!confirmed) return;

        var deletionResult =
            LocalModlistFileService.Delete(entries);

        if (deletionResult.Errors.Count > 0)
        {
            var deletionFailureMessage = LocalModlistDialogTextBuilder.BuildDeletionFailureMessage(deletionResult.Errors);
            await _confirmationService.NotifyAsync(
                    deletionFailureMessage,
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        else if (deletionResult.DeletedCount > 0)
        {
            var statusMessage =
                LocalModlistDialogTextBuilder.BuildDeletionStatusMessage(deletionResult.DeletedCount);
            if (!string.IsNullOrWhiteSpace(statusMessage))
                _viewModel?.ReportStatus(statusMessage);
        }

        RefreshLocalModlists(true, Array.Empty<string>());
    }

    private async void ModifyLocalModlistButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedLocalModlists.Count != 1) return;

        var entry = _selectedLocalModlists[0];
        if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath)) return;

        var dialog = new LocalModlistEditDialog(this, entry.DisplayName, entry.Description, entry.Version,
            entry.GameVersion);
        var dialogResult = dialog.ShowDialog();
        if (dialogResult != true) return;

        var updatedName = dialog.ModlistName?.Trim();
        var updatedDescription =
            string.IsNullOrWhiteSpace(dialog.ModlistDescription)
                ? null
                : dialog.ModlistDescription!.Trim();
        var updatedVersion =
            string.IsNullOrWhiteSpace(dialog.ModlistVersion)
                ? null
                : dialog.ModlistVersion!.Trim();
        var updatedGameVersion =
            string.IsNullOrWhiteSpace(dialog.ModlistGameVersion)
                ? null
                : dialog.ModlistGameVersion!.Trim();

        var updateResult =
            LocalModlistFileService.UpdateMetadata(
                entry.FilePath,
                updatedName,
                updatedDescription,
                updatedVersion,
                updatedGameVersion);

        if (!updateResult.Success)
        {
            var updateFailureMessage = LocalModlistDialogTextBuilder.BuildUpdateFailureMessage(updateResult);

            await _confirmationService.NotifyAsync(
                    updateFailureMessage,
                    "Modify Modlist",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        var statusName = string.IsNullOrWhiteSpace(updatedName) ? entry.DisplayName : updatedName!;
        _viewModel?.ReportStatus($"Updated modlist \"{statusName}\".");
        RefreshLocalModlists(true, new[] { entry.FilePath });
    }

    private void RefreshLocalModlists(bool force, IReadOnlyCollection<string>? preferredSelection = null)
    {
        RefreshLocalModlistsAsync(force, preferredSelection).GetAwaiter().GetResult();
    }

    private async Task RefreshLocalModlistsAsync(bool force, IReadOnlyCollection<string>? preferredSelection = null)
    {
        if (!force && _localModlistsLoaded) return;

        LocalModlistCatalogResult catalogResult;

        try
        {
            var directory = EnsureModListDirectory();
            catalogResult =
                LocalModlistCatalogService.BuildCatalog(directory);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            await _confirmationService.NotifyAsync(
                    LocalModlistDialogTextBuilder.BuildReadFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);

            catalogResult = LocalModlistCatalogResult.Empty;
        }

        _viewModel?.ReplaceLocalModlists(catalogResult.Entries);
        _localModlistsLoaded = true;

        var preferred = preferredSelection is not null
            ? new HashSet<string>(preferredSelection.Where(path => !string.IsNullOrWhiteSpace(path)),
                StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(_selectedLocalModlists
                .Where(entry => !string.IsNullOrWhiteSpace(entry?.FilePath))
                .Select(entry => entry.FilePath), StringComparer.OrdinalIgnoreCase);

        if (LocalModlistsDataGrid is not null)
        {
            LocalModlistsDataGrid.SelectedItems.Clear();

            if (preferred.Count > 0)
            {
                foreach (var item in LocalModlistsDataGrid.Items.OfType<LocalModlistListEntry>())
                    if (!string.IsNullOrWhiteSpace(item?.FilePath) && preferred.Contains(item.FilePath))
                        LocalModlistsDataGrid.SelectedItems.Add(item);
            }
        }

        if (LocalModlistsDataGrid is not null && LocalModlistsDataGrid.SelectedItems.Count > 0)
        {
            var selected = LocalModlistsDataGrid.SelectedItems.OfType<LocalModlistListEntry>().ToList();
            SetLocalModlistSelection(selected);
        }
        else
        {
            SetLocalModlistSelection(Array.Empty<LocalModlistListEntry>());
        }

        if (catalogResult.Errors.Count > 0)
        {
            var catalogFailureMessage = LocalModlistDialogTextBuilder.BuildCatalogFailureMessage(catalogResult.Errors);

            await _confirmationService.NotifyAsync(
                    catalogFailureMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
    }

    private void SetLocalModlistSelection(IReadOnlyList<LocalModlistListEntry> selection)
    {
        _selectedLocalModlists.Clear();

        if (selection is not null)
            foreach (var entry in selection)
                if (entry is not null)
                    _selectedLocalModlists.Add(entry);

        var primary = _selectedLocalModlists.FirstOrDefault();

        if (SelectedLocalModlistTitle is not null)
            SelectedLocalModlistTitle.Text = primary?.DisplayName ?? string.Empty;

        if (SelectedLocalModlistDescription is not null)
            SelectedLocalModlistDescription.Text = primary?.Description ?? string.Empty;

        if (InstallLocalModlistButton is not null)
        {
            if (_selectedLocalModlists.Count == 1 && primary is not null)
                InstallLocalModlistButton.ToolTip = $"Install \"{primary.DisplayName}\"";
            else
                InstallLocalModlistButton.ToolTip = null;
        }

        UpdateLocalModlistControlsEnabledState();
    }

    private void UpdateLocalModlistControlsEnabledState()
    {
        var hasSelection = _selectedLocalModlists.Count > 0;
        var hasSingleSelection = _selectedLocalModlists.Count == 1;

        if (DeleteLocalModlistsButton is not null)
            DeleteLocalModlistsButton.IsEnabled = hasSelection;

        if (ModifyLocalModlistButton is not null)
            ModifyLocalModlistButton.IsEnabled = _selectedLocalModlists.Count == 1;

        if (InstallLocalModlistButton is not null)
            InstallLocalModlistButton.IsEnabled = hasSingleSelection;
    }
}
