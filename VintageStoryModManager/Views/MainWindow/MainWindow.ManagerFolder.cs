#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

using WinForms = System.Windows.Forms;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ManagerDataFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var directory = GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(directory))
        {
            WpfMessageBox.Show(
                "The manager data folder is not available on this system.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to open the manager data folder:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        FolderOpeningHelper.OpenFolder(directory, "manager data");
    }

    private void ChangeManagerFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var currentFolder =
            ManagerDataFolderRelocationService.GetCurrentManagerFolder();

        if (string.IsNullOrWhiteSpace(currentFolder))
        {
            WpfMessageBox.Show(
                "Cannot determine the current manager data folder location.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var dialog = new ChangeManagerFolderDialog(this, currentFolder);
        var dialogResult = dialog.ShowDialog();

        if (dialogResult != true)
            return;

        if (dialog.Result == ChangeManagerFolderDialogResult.Reset)
        {
            HandleResetManagerFolder(currentFolder);
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
            var overwriteMessage =
                $"The folder \"{newManagerFolder}\" already exists.\n\n" +
                "Do you want to merge with the existing folder?\n" +
                "(Existing files with the same name will be overwritten)";

            var overwriteResult = WpfMessageBox.Show(
                overwriteMessage,
                "Folder Already Exists",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (overwriteResult != MessageBoxResult.Yes)
                return;
        }

        var confirmMessage =
            $"Move manager folder from:\n{currentFolder}\n\n" +
            $"To:\n{newManagerFolder}\n\n" +
            "The application will restart after the move is complete.\n\n" +
            "Continue?";

        var confirmResult = WpfMessageBox.Show(
            confirmMessage,
            "Confirm Move",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        try
        {
            var moveResult =
                ManagerDataFolderRelocationService.MoveToCustomFolder(
                    currentFolder,
                    newManagerFolder);

            if (moveResult == ManagerFolderMoveResult.SameLocation)
            {
                WpfMessageBox.Show(
                    "The selected location is the same as the current location.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var successMessage =
                $"Manager folder moved successfully to:\n{newManagerFolder}\n\n" +
                "The application will now restart.";

            WpfMessageBox.Show(
                successMessage,
                "Move Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RestartApplication();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to move the manager folder:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void HandleResetManagerFolder(string currentFolder)
    {
        var defaultFolder =
            ManagerDataFolderRelocationService.GetDefaultManagerFolder();

        if (string.IsNullOrWhiteSpace(defaultFolder))
        {
            WpfMessageBox.Show(
                "Cannot determine the default manager data folder location.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (ManagerDataFolderRelocationService.IsSameLocation(
                currentFolder,
                defaultFolder))
        {
            WpfMessageBox.Show(
                $"Already using the default manager folder location:\n{defaultFolder}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirmMessage =
            "Reset manager folder to default location?\n\n" +
            $"Current location:\n{currentFolder}\n\n" +
            $"Default location:\n{defaultFolder}\n\n" +
            "The manager will:\n" +
            "• Move all configuration files, cached mods, backups, and presets\n" +
            "• Update the configuration to use the default location\n" +
            "• Require a restart to complete the change\n\n" +
            "Note: The Firebase authentication backup (SVSM Backup folder) " +
            "will remain in its original location.\n\n" +
            "Continue?";

        var confirmResult = WpfMessageBox.Show(
            confirmMessage,
            "Reset to Default Location",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        if (ManagerDataFolderRelocationService.DirectoryExists(defaultFolder))
        {
            var overwriteMessage =
                $"The default folder \"{defaultFolder}\" already exists.\n\n" +
                "Do you want to merge with the existing folder?\n" +
                "(Existing files with the same name will be overwritten)";

            var overwriteResult = WpfMessageBox.Show(
                overwriteMessage,
                "Folder Already Exists",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (overwriteResult != MessageBoxResult.Yes)
                return;
        }

        try
        {
            ManagerDataFolderRelocationService.ResetToDefaultFolder(
                currentFolder,
                defaultFolder);

            var successMessage =
                $"Manager folder reset to default location:\n{defaultFolder}\n\n" +
                "The application will now restart.";

            WpfMessageBox.Show(
                successMessage,
                "Reset Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RestartApplication();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to reset the manager folder:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

    private static string? GetManagerDataDirectory()
    {
        return ModCacheLocator.GetManagerDataDirectory();
    }
}
