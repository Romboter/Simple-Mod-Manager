#nullable enable

using SimpleVsManager.Cloud;
using System.Net.Http;
using System.Threading;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox =
    VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task ShowCloudModlistManagementDialogAsync(FirebaseModlistStore store)
        {
            var entries =
                await CloudModlistManagementService.LoadEntriesAsync(
                    store);
            if (entries.Count == 0)
            {
                WpfMessageBox.Show(
                    "You do not have any cloud modlists saved.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new CloudModlistManagementDialog(
                this,
                entries,
                () => CloudModlistManagementService.LoadEntriesAsync(store),
                (entry, newName) => RenameCloudModlistAsync(store, entry, newName),
                entry => DeleteCloudModlistAsync(store, entry));

            dialog.ShowDialog();
        }

    private async Task<bool> RenameCloudModlistAsync(
            FirebaseModlistStore store,
            CloudModlistManagementEntry entry,
            string newName)
        {
            var result =
                await CloudModlistManagementService.RenameAsync(
                    store,
                    entry,
                    newName);

            switch (result.Status)
            {
                case CloudModlistRenameStatus.InvalidName:
                    return false;

                case CloudModlistRenameStatus.LoadFailed:
                    StatusLogService.AppendStatus(
                        $"Failed to load cloud modlist for rename: " +
                        $"{result.ErrorMessage}",
                        true);

                    WpfMessageBox.Show(
                        $"Failed to load the cloud modlist before renaming:\n" +
                        result.ErrorMessage,
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;

                case CloudModlistRenameStatus.ContentUnavailable:
                    WpfMessageBox.Show(
                        "The selected cloud modlist could not be loaded.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return false;

                case CloudModlistRenameStatus.InvalidContent:
                    StatusLogService.AppendStatus(
                        $"Failed to update cloud modlist name: " +
                        $"{result.ErrorMessage}",
                        true);

                    WpfMessageBox.Show(
                        $"The cloud modlist data is invalid and could not " +
                        $"be renamed:\n{result.ErrorMessage}",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;

                case CloudModlistRenameStatus.SaveFailed:
                    StatusLogService.AppendStatus(
                        $"Failed to rename cloud modlist: " +
                        $"{result.ErrorMessage}",
                        true);

                    WpfMessageBox.Show(
                        $"Failed to rename the cloud modlist:\n" +
                        result.ErrorMessage,
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;

                case CloudModlistRenameStatus.Success:
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unexpected cloud modlist rename result.");
            }

            var slotLabel =
                CloudModlistHelper.FormatCloudSlotLabel(
                    entry.SlotKey);

            _viewModel?.ReportStatus(
                $"Renamed cloud modlist in {slotLabel} to " +
                $"\"{result.Name}\".");

            await UpdateCloudModlistsAfterChangeAsync();
            return true;
        }

    private async Task<bool> DeleteCloudModlistAsync(
            FirebaseModlistStore store,
            CloudModlistManagementEntry entry)
        {
            var result =
                await CloudModlistManagementService.DeleteAsync(
                    store,
                    entry);

            switch (result.Status)
            {
                case CloudModlistDeleteStatus.NetworkFailed:
                    StatusLogService.AppendStatus(
                        $"Failed to delete cloud modlist: " +
                        $"{result.ErrorMessage}",
                        true);

                    WpfMessageBox.Show(
                        $"Failed to delete the cloud modlist:\n" +
                        result.ErrorMessage,
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;

                case CloudModlistDeleteStatus.InvalidRequest:
                    StatusLogService.AppendStatus(
                        $"Invalid request while deleting cloud modlist: " +
                        $"{result.ErrorMessage}",
                        true);

                    WpfMessageBox.Show(
                        $"Failed to delete the cloud modlist:\n" +
                        result.ErrorMessage,
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return false;

                case CloudModlistDeleteStatus.Success:
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unexpected cloud modlist delete result.");
            }

            var slotLabel =
                CloudModlistHelper.FormatCloudSlotLabel(
                    entry.SlotKey);

            _viewModel?.ReportStatus(
                $"Deleted cloud modlist from {slotLabel}.");

            await UpdateCloudModlistsAfterChangeAsync();
            return true;
        }

    private async Task DeleteAllCloudModlistsAndAuthorizationAsync(FirebaseModlistStore store,
            bool showCompletionMessage = true)
        {
            await CloudModlistManagementService
                .DeleteAllUserDataAndAuthorizationAsync(
                    store,
                    CancellationToken.None);

            DeleteFirebaseAuthFiles();

            _cloudModlistStore = null;
            _cloudModlistsLoaded = false;

            SetCloudModlistSelection(null);
            _viewModel?.ReplaceCloudModlists(null);
            if (CloudModlistsDataGrid is not null) CloudModlistsDataGrid.SelectedItem = null;

            StatusLogService.AppendStatus("Deleted all cloud modlists and Firebase authorization.", false);
            _viewModel?.ReportStatus("Deleted all cloud modlists and Firebase authorization.");

            if (showCompletionMessage)
                WpfMessageBox.Show(
                    this,
                    "Cloud modlists and Firebase authorization have been deleted.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
        }

    private async Task UpdateCloudModlistsAfterChangeAsync()
        {
            if (_viewModel?.IsViewingModlistTab == true)
                await RefreshCloudModlistsAsync(true);
            else
                _cloudModlistsLoaded = false;
        }

    private async void ModifyCloudModlistsButton_OnClick(object sender, RoutedEventArgs e)
        {
            await ExecuteCloudOperationAsync(async store => { await ShowCloudModlistManagementDialogAsync(store); },
                "manage your cloud modlists");
        }
}
