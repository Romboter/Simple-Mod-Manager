#nullable enable

using System.Diagnostics;
using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

using WinForms = System.Windows.Forms;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void ManagerDataFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var directory = ModCacheLocator.GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(directory))
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.ManagerDataFolderUnavailableMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.BuildOpenManagerFolderFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        FolderOpeningHelper.OpenFolder(directory, "manager data");
    }

    private async void ChangeManagerFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var currentFolder =
            ManagerDataFolderRelocationService.GetCurrentManagerFolder();

        if (string.IsNullOrWhiteSpace(currentFolder))
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.CannotDetermineCurrentManagerFolderMessage,
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        var dialog = new ChangeManagerFolderDialog(this, currentFolder);
        var dialogResult = dialog.ShowDialog();

        if (dialogResult != true)
            return;

        if (dialog.Result == ChangeManagerFolderDialogResult.Reset)
        {
            await HandleResetManagerFolderAsync(currentFolder).ConfigureAwait(true);
            return;
        }

        if (dialog.Result != ChangeManagerFolderDialogResult.Yes)
            return;

        using var folderDialog = new WinForms.FolderBrowserDialog
        {
            Description =
                "Select the new location for the \"Simple VS Manager\" folder.\n" +
                "The folder will be created if it doesn't exist.",
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        if (folderDialog.ShowDialog() != WinForms.DialogResult.OK)
            return;

        var newParentFolder = folderDialog.SelectedPath;
        if (string.IsNullOrWhiteSpace(newParentFolder))
            return;

        var newManagerFolder =
            ManagerDataFolderRelocationService.GetCustomManagerFolder(
                newParentFolder);

        if (ManagerDataFolderRelocationService.DirectoryExists(newManagerFolder) &&
            !ManagerDataFolderRelocationService.IsSameLocation(
                currentFolder,
                newManagerFolder))
        {
            var overwriteResult = await _confirmationService.ConfirmAsync(
                    ManagerFolderDialogTextBuilder.BuildExistingFolderPrompt(
                        newManagerFolder),
                    "Folder Already Exists",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);

            if (!overwriteResult) return;
        }

        var confirmResult = await _confirmationService.ConfirmAsync(
                ManagerFolderDialogTextBuilder.BuildMoveConfirmationMessage(
                    currentFolder,
                    newManagerFolder),
                "Confirm Move",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!confirmResult) return;

        try
        {
            var moveResult =
                ManagerDataFolderRelocationService.MoveToCustomFolder(
                    currentFolder,
                    newManagerFolder);

            if (moveResult == ManagerFolderMoveResult.SameLocation)
            {
                await _confirmationService.NotifyAsync(
                        ManagerFolderDialogTextBuilder.SameLocationMessage,
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);
                return;
            }

            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.BuildMoveSuccessMessage(
                        newManagerFolder),
                    "Move Complete",
                    DialogSeverity.Information)
                .ConfigureAwait(true);

            RestartApplication();
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.BuildMoveFailureMessage(ex.Message),
                    "Error",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async Task HandleResetManagerFolderAsync(string currentFolder)
    {
        var defaultFolder =
            ManagerDataFolderRelocationService.GetDefaultManagerFolder();

        if (string.IsNullOrWhiteSpace(defaultFolder))
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.CannotDetermineDefaultManagerFolderMessage,
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        if (ManagerDataFolderRelocationService.IsSameLocation(
                currentFolder,
                defaultFolder))
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.BuildAlreadyDefaultMessage(
                        defaultFolder),
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var confirmResult = await _confirmationService.ConfirmAsync(
                ManagerFolderDialogTextBuilder.BuildResetConfirmationMessage(
                    currentFolder,
                    defaultFolder),
                "Reset to Default Location",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (!confirmResult) return;

        if (ManagerDataFolderRelocationService.DirectoryExists(defaultFolder))
        {
            var overwriteResult = await _confirmationService.ConfirmAsync(
                    ManagerFolderDialogTextBuilder.BuildExistingFolderPrompt(
                        defaultFolder),
                    "Folder Already Exists",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);

            if (!overwriteResult) return;
        }

        try
        {
            ManagerDataFolderRelocationService.ResetToDefaultFolder(
                currentFolder,
                defaultFolder);

            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.BuildResetSuccessMessage(
                        defaultFolder),
                    "Reset Complete",
                    DialogSeverity.Information)
                .ConfigureAwait(true);

            RestartApplication();
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ManagerFolderDialogTextBuilder.BuildResetFailureMessage(ex.Message),
                    "Error",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private static void RestartApplication()
    {
        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable))
        {
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule != null)
            {
                currentExecutable = mainModule.FileName;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentExecutable))
        {
            try
            {
                Process.Start(currentExecutable);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception)
            {
                // If restart fails, just close the application
                System.Windows.Application.Current.Shutdown();
            }
        }
        else
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

}
