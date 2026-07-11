#nullable enable
using SimpleVsManager.Cloud;
using System.IO;
using System.Security;
using System.Windows;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async Task<bool> EnsureCloudModlistsConsentAsync()
    {
        var message =
            "In this tab you can easily save and load Modlists from an online database (Google Firebase), for free." +
            Environment.NewLine + Environment.NewLine +
            "When you continue, Simple VS Manager will create a firebase-auth.json (basically just a code that identifies you as the owner of your uploaded modlists) file in its configuration folder. " +
            "If you lose this file you will not be able to delete or modify your uploaded online modlists." +
            Environment.NewLine + Environment.NewLine +
            "You will not need to sign in or provide any account information or do anything really :) Press OK to continue and never show this again!";

        return await EnsureFirebaseAuthConsentAsync(message).ConfigureAwait(true);
    }

    private async Task<bool> EnsureFirebaseAuthConsentAsync(string message)
    {
        var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
        if (string.IsNullOrWhiteSpace(stateFilePath)) return true;

        if (File.Exists(stateFilePath))
        {
            EnsureFirebaseAuthBackedUpIfAvailable();
            return true;
        }

        return await _confirmationService.ConfirmOkCancelAsync(
                message,
                "Simple VS Manager",
                DialogSeverity.Information,
                cancelText: "No thanks")
            .ConfigureAwait(true);
    }

    private async Task<bool> EnsureUserReportVotingConsentAsync()
    {
        var message =
            "To enable voting, Simple VS Manager will create a firebase-auth.json (basically just a code that identifies you as the owner of your mod compatibility votes) file in its configuration folder. " +
            "If you lose this file you will not be able to manage or remove your mod compatibility votes." +
            Environment.NewLine + Environment.NewLine +
            "You will not need to sign in or provide any account information or do anything really :) Press OK to continue and never show this again!";

        return await EnsureFirebaseAuthConsentAsync(message).ConfigureAwait(true);
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

    private async void RestoreFirebaseAuthBackupMenuItem_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        const string confirmationMessage =
            "This will copy the firebase-auth.json file from the SVSM Backup folder " +
            "(AppData/Local/SVSM Backup) into the Simple VS Manager folder and replace the current file.\n\n" +
            "Use this if you lost access to online modlists or votes after moving or deleting files.\n\n" +
            "Continue?";

        var confirmation = await _confirmationService.ConfirmOkCancelAsync(
                confirmationMessage,
                "Simple VS Manager",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (!confirmation) return;

        try
        {
            var restoreResult =
                FirebaseAuthFileService.RestoreFirebaseAuthBackup();

            switch (restoreResult)
            {
                case FirebaseAuthRestoreResult.BackupLocationUnavailable:
                    await _confirmationService.NotifyAsync(
                            "Could not determine the location of the firebase-auth.json backup.",
                            "Simple VS Manager",
                            DialogSeverity.Error)
                        .ConfigureAwait(true);
                    break;

                case FirebaseAuthRestoreResult.BackupNotFound:
                    await _confirmationService.NotifyAsync(
                            "No firebase-auth.json backup was found in the SVSM Backup folder.",
                            "Simple VS Manager",
                            DialogSeverity.Warning)
                        .ConfigureAwait(true);
                    break;

                case FirebaseAuthRestoreResult.StateLocationUnavailable:
                    await _confirmationService.NotifyAsync(
                            "Could not determine the Simple VS Manager folder for firebase-auth.json.",
                            "Simple VS Manager",
                            DialogSeverity.Error)
                        .ConfigureAwait(true);
                    break;

                case FirebaseAuthRestoreResult.Restored:
                    await _confirmationService.NotifyAsync(
                            "Restored firebase-auth.json from the SVSM Backup folder.",
                            "Simple VS Manager",
                            DialogSeverity.Information)
                        .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    CloudAuthDialogTextBuilder.BuildRestoreFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }
}
