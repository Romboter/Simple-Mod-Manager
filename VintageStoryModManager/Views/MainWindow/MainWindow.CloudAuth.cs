#nullable enable
using SimpleVsManager.Cloud;
using System.IO;
using System.Security;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private bool EnsureCloudModlistsConsent()
    {
        var message =
            "In this tab you can easily save and load Modlists from an online database (Google Firebase), for free." +
            Environment.NewLine + Environment.NewLine +
            "When you continue, Simple VS Manager will create a firebase-auth.json (basically just a code that identifies you as the owner of your uploaded modlists) file in its configuration folder. " +
            "If you lose this file you will not be able to delete or modify your uploaded online modlists." +
            Environment.NewLine + Environment.NewLine +
            "You will not need to sign in or provide any account information or do anything really :) Press OK to continue and never show this again!";

        return EnsureFirebaseAuthConsent(message);
    }

    private bool EnsureFirebaseAuthConsent(string message)
    {
        var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
        if (string.IsNullOrWhiteSpace(stateFilePath)) return true;

        if (File.Exists(stateFilePath))
        {
            EnsureFirebaseAuthBackedUpIfAvailable();
            return true;
        }

        var buttonOverrides = new MessageDialogButtonContentOverrides
        {
            Cancel = "No thanks"
        };

        var result = WpfMessageBox.Show(
            this,
            message,
            "Simple VS Manager",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information,
            buttonContentOverrides: buttonOverrides);

        return result == MessageBoxResult.OK;
    }

    private bool EnsureUserReportVotingConsent()
    {
        var message =
            "To enable voting, Simple VS Manager will create a firebase-auth.json (basically just a code that identifies you as the owner of your mod compatibility votes) file in its configuration folder. " +
            "If you lose this file you will not be able to manage or remove your mod compatibility votes." +
            Environment.NewLine + Environment.NewLine +
            "You will not need to sign in or provide any account information or do anything really :) Press OK to continue and never show this again!";

        return EnsureFirebaseAuthConsent(message);
    }

    private void EnsureFirebaseAuthBackedUpIfAvailable()
    {
        var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
        if (string.IsNullOrWhiteSpace(stateFilePath) || !File.Exists(stateFilePath)) return;

        var dataDirectory = _dataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory)) return;

        try
        {
            FirebaseAuthFileService.BackupAuthStateToDataDirectory(stateFilePath, dataDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _modActivityLoggingService.LogError("Failed to back up Firebase auth state", ex);
            StatusLogService.AppendStatus($"Failed to back up Firebase auth state: {ex.Message}", true);
        }
    }

    private void DeleteFirebaseAuthFiles()
    {
        var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
        if (!string.IsNullOrWhiteSpace(stateFilePath)) FirebaseAuthFileService.TryDeleteFirebaseAuthFile(stateFilePath);

        var dataDirectory = _dataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory)) return;

        var backupPath = FirebaseAuthFileService.GetDataDirectoryBackupPath(dataDirectory);
        FirebaseAuthFileService.TryDeleteFirebaseAuthFile(backupPath);
    }

    private void RestoreFirebaseAuthBackupMenuItem_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        const string confirmationMessage =
            "This will copy the firebase-auth.json file from the SVSM Backup folder " +
            "(AppData/Local/SVSM Backup) into the Simple VS Manager folder and replace the current file.\n\n" +
            "Use this if you lost access to online modlists or votes after moving or deleting files.\n\n" +
            "Continue?";

        var confirmation = WpfMessageBox.Show(
            this,
            confirmationMessage,
            "Simple VS Manager",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.OK) return;

        try
        {
            var restoreResult =
                FirebaseAuthFileService.RestoreFirebaseAuthBackup();

            switch (restoreResult)
            {
                case FirebaseAuthRestoreResult.BackupLocationUnavailable:
                    WpfMessageBox.Show(
                        this,
                        "Could not determine the location of the firebase-auth.json backup.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;

                case FirebaseAuthRestoreResult.BackupNotFound:
                    WpfMessageBox.Show(
                        this,
                        "No firebase-auth.json backup was found in the SVSM Backup folder.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;

                case FirebaseAuthRestoreResult.StateLocationUnavailable:
                    WpfMessageBox.Show(
                        this,
                        "Could not determine the Simple VS Manager folder for firebase-auth.json.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;

                case FirebaseAuthRestoreResult.Restored:
                    WpfMessageBox.Show(
                        this,
                        "Restored firebase-auth.json from the SVSM Backup folder.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            SecurityException)
        {
            WpfMessageBox.Show(
                this,
                $"Failed to restore firebase-auth.json: {ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
