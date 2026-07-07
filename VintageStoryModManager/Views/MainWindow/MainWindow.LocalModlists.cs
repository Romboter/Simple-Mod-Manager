#nullable enable

using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox =
    VintageStoryModManager.Services.ModManagerMessageBox;

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

    private void SaveLocalModlistButton_OnClick(object sender, RoutedEventArgs e)
        {
            var preservedSelection = _selectedLocalModlists
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.FilePath))
                .Select(entry => entry.FilePath)
                .ToList();

            if (TrySaveModlist(null, out var savedFilePath))
            {
                if (!string.IsNullOrWhiteSpace(savedFilePath)) preservedSelection.Add(savedFilePath);
                RefreshLocalModlists(true, preservedSelection);
            }
        }

    private async void InstallLocalModlistButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectedLocalModlists.Count != 1) return;

            var entry = _selectedLocalModlists[0];

            if (string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
            {
                WpfMessageBox.Show(
                    "The selected modlist file could not be found. It may have been moved or deleted.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                RefreshLocalModlists(true);
                return;
            }

            await LoadModlistFromFileAsync(entry.FilePath).ConfigureAwait(true);
        }

    private void OpenModlistsFolderButton_OnClick(object sender, RoutedEventArgs e)
        {
            string directory;
            try
            {
                directory = EnsureModListDirectory();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                WpfMessageBox.Show($"Failed to open the Modlists folder:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            FolderOpeningHelper.OpenFolder(directory, "Modlists");
        }

    private void DeleteLocalModlistsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectedLocalModlists.Count == 0) return;

            var entries = _selectedLocalModlists
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.FilePath))
                .ToList();

            if (entries.Count == 0) return;

            string message;
            if (entries.Count == 1)
            {
                var name = entries[0].DisplayName;
                message = $"Are you sure you want to delete the modlist \"{name}\"? This cannot be undone.";
            }
            else
            {
                message = $"Are you sure you want to delete the {entries.Count} selected modlists? This cannot be undone.";
            }

            var confirmation = WpfMessageBox.Show(
                this,
                message,
                "Delete Modlists",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes) return;

            var deletionResult =
                LocalModlistFileService.Delete(entries);

            if (deletionResult.Errors.Count > 0)
            {
                var summary = string.Join(
                    "\n",
                    deletionResult.Errors.Select(error => $"• {error}"));

                WpfMessageBox.Show(
                    this,
                    "Some modlists could not be deleted:\n" + summary,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else if (deletionResult.DeletedCount > 0)
            {
                var statusMessage =
                    deletionResult.DeletedCount == 1
                        ? "Deleted local modlist."
                        : $"Deleted {deletionResult.DeletedCount} local modlists.";

                _viewModel?.ReportStatus(statusMessage);
            }

            RefreshLocalModlists(true, Array.Empty<string>());
        }

    private void ModifyLocalModlistButton_OnClick(object sender, RoutedEventArgs e)
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
                var errorMessage =
                    updateResult.ErrorMessage ??
                    "The modlist could not be updated.";

                var message =
                    updateResult.FailureStage ==
                    LocalModlistUpdateFailureStage.Read
                        ? $"Failed to read the modlist:\n{errorMessage}"
                        : updateResult.FailureStage ==
                          LocalModlistUpdateFailureStage.Write
                            ? $"Failed to update the modlist:\n{errorMessage}"
                            : errorMessage;

                WpfMessageBox.Show(
                    this,
                    message,
                    "Modify Modlist",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var statusName = string.IsNullOrWhiteSpace(updatedName) ? entry.DisplayName : updatedName!;
            _viewModel?.ReportStatus($"Updated modlist \"{statusName}\".");
            RefreshLocalModlists(true, new[] { entry.FilePath });
        }

    private void RefreshLocalModlists(bool force, IReadOnlyCollection<string>? preferredSelection = null)
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
                WpfMessageBox.Show(
                    $"Failed to read local modlists:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

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
                var summary = string.Join(
                    "\n",
                    catalogResult.Errors.Select(error => $"• {error}"));

                WpfMessageBox.Show(
                    this,
                    "Some local modlists could not be loaded:\n" + summary,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
