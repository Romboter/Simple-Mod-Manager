#nullable enable

using SimpleVsManager.Cloud;
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
            await _confirmationService.NotifyAsync(
                    CloudManagementDialogTextBuilder.NoCloudModlistsSavedMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
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

                await _confirmationService.NotifyAsync(
                        CloudManagementDialogTextBuilder.BuildRenameLoadFailureMessage(
                            result.ErrorMessage),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);

                return false;

            case CloudModlistRenameStatus.ContentUnavailable:
                await _confirmationService.NotifyAsync(
                        CloudManagementDialogTextBuilder.ContentUnavailableMessage,
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);

                return false;

            case CloudModlistRenameStatus.InvalidContent:
                StatusLogService.AppendStatus(
                    $"Failed to update cloud modlist name: " +
                    $"{result.ErrorMessage}",
                    true);

                await _confirmationService.NotifyAsync(
                        CloudManagementDialogTextBuilder.BuildInvalidRenameContentMessage(
                            result.ErrorMessage),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);

                return false;

            case CloudModlistRenameStatus.SaveFailed:
                StatusLogService.AppendStatus(
                    $"Failed to rename cloud modlist: " +
                    $"{result.ErrorMessage}",
                    true);

                await _confirmationService.NotifyAsync(
                        CloudManagementDialogTextBuilder.BuildRenameSaveFailureMessage(
                            result.ErrorMessage),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);

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
            CloudManagementDialogTextBuilder.BuildRenamedStatusMessage(
                slotLabel,
                result.Name));

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

                await _confirmationService.NotifyAsync(
                        CloudManagementDialogTextBuilder.BuildDeleteFailureMessage(
                            result.ErrorMessage),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);

                return false;

            case CloudModlistDeleteStatus.InvalidRequest:
                StatusLogService.AppendStatus(
                    $"Invalid request while deleting cloud modlist: " +
                    $"{result.ErrorMessage}",
                    true);

                await _confirmationService.NotifyAsync(
                        CloudManagementDialogTextBuilder.BuildDeleteFailureMessage(
                            result.ErrorMessage),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);

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
            CloudManagementDialogTextBuilder.BuildDeletedStatusMessage(slotLabel));

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

        StatusLogService.AppendStatus(
            CloudManagementDialogTextBuilder.DeletedAllCloudModlistsAndAuthorizationMessage,
            false);
        _viewModel?.ReportStatus(
            CloudManagementDialogTextBuilder.DeletedAllCloudModlistsAndAuthorizationMessage);

        if (showCompletionMessage)
        {
            await _confirmationService.NotifyAsync(
                    CloudManagementDialogTextBuilder.DeletedAllCloudModlistsAndAuthorizationMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        }
    }

    private async void DeleteCloudAuthMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        const string confirmationMessage =
            "This will remove all your online modlists and delete your authorization - good for resetting if something has gone wrong. Visit the Modlists (Beta) tab again to get a fresh firebase-auth";

        var result = WpfMessageBox.Show(
            this,
            confirmationMessage,
            "Simple VS Manager",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        await ExecuteCloudOperationAsync(
            store => DeleteAllCloudModlistsAndAuthorizationAsync(store),
            "delete all cloud modlists and Firebase authorization");
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
