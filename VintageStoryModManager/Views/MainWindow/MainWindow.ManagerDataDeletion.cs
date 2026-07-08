#nullable enable

using System.Windows;
using VintageStoryModManager.Services;


namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void DeleteAllManagerFilesMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var confirmation = await _confirmationService.ConfirmAsync(
                ManagerDataDeletionDialogTextBuilder.DeleteAllManagerFilesConfirmationMessage,
                "Simple VS Manager",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!confirmation) return;

        await ExecuteCloudOperationAsync(
            store => DeleteAllCloudModlistsAndAuthorizationAsync(store, false),
            "delete the Firebase user and cloud data");

        var dataDirectory = _dataDirectory;
        var deletionResult = await Task.Run(() => ManagerDataDeletionService.DeleteAllManagerFiles(dataDirectory)).ConfigureAwait(true);

        if (deletionResult.DeletedPaths.Count == 0 && deletionResult.FailedPaths.Count == 0)
        {
            await _confirmationService.NotifyAsync(
                    ManagerDataDeletionDialogTextBuilder.NoManagerFilesFoundMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            SwitchToInstalledModsTab();
            return;
        }

        await _confirmationService.NotifyAsync(
                ManagerDataDeletionDialogTextBuilder.BuildDeletionResultMessage(
                    deletionResult.DeletedPaths,
                    deletionResult.FailedPaths),
                "Simple VS Manager",
                deletionResult.FailedPaths.Count > 0
                    ? DialogSeverity.Warning
                    : DialogSeverity.Information)
            .ConfigureAwait(true);

        SwitchToInstalledModsTab();
    }
}
