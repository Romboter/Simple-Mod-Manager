#nullable enable
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;
using VintageStoryModManager.Helpers;
using DataFolderBackupProgress = VintageStoryModManager.Services.DataBackupProgress;
using DataFolderBackupSummary = VintageStoryModManager.Services.DataBackupSummary;
using WinForms = System.Windows.Forms;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void RestoreDataFolderMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        menuItem.Items.Clear();

        var openDirectoryMenuItem = new MenuItem
        {
            Header = "Open data backup directory..."
        };
        openDirectoryMenuItem.Click += OpenDataBackupDirectoryMenuItem_OnClick;
        menuItem.Items.Add(openDirectoryMenuItem);

        var changeBackupLocationMenuItem = new MenuItem
        {
            Header = "Change backup location..."
        };
        changeBackupLocationMenuItem.Click += ChangeBackupLocationMenuItem_OnClick;
        menuItem.Items.Add(changeBackupLocationMenuItem);

        var deleteBackupsMenuItem = new MenuItem
        {
            Header = "Delete all data folder backups",
            IsEnabled = false
        };
        deleteBackupsMenuItem.Click += DeleteDataFolderBackupsMenuItem_OnClick;
        menuItem.Items.Add(deleteBackupsMenuItem);
        menuItem.Items.Add(new Separator());

        IReadOnlyList<DataFolderBackupSummary> backups;
        try
        {
            backups = _dataFolderBackupCoordinator.GetAvailableBackups();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to access data backups: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Backups unavailable",
                IsEnabled = false
            });
            return;
        }

        var dataDirectory = _dataDirectory;
        DataFolderBackupSummary[] filteredBackups;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            filteredBackups = Array.Empty<DataFolderBackupSummary>();
        }
        else
        {
            filteredBackups = backups
                .Where(summary => PathRelationshipHelper.IsSameDirectory(summary.SourceDataDirectory, dataDirectory))
                .ToArray();
        }

        var (_, normalizedInstalledVersion) = VintageStoryVersionLocator.GetNormalizedInstalledVersion(_gameDirectory);
        deleteBackupsMenuItem.IsEnabled = !string.IsNullOrWhiteSpace(dataDirectory)
                                          && !string.IsNullOrWhiteSpace(normalizedInstalledVersion)
                                          && filteredBackups.Length > 0;

        if (filteredBackups.Length == 0)
        {
            var header = string.IsNullOrWhiteSpace(dataDirectory)
                ? "Set VintagestoryData folder to restore backups"
                : "No backups for this VintagestoryData folder";
            menuItem.Items.Add(new MenuItem
            {
                Header = header,
                IsEnabled = false
            });
            return;
        }

        var displayedBackups = filteredBackups
            .Take(MaxDataBackupsMenuItems)
            .ToArray();

        foreach (var backup in displayedBackups)
        {
            var timestamp = backup.CreatedOnUtc.ToLocalTime()
                .ToString("dd MMM yyyy '•' HH:mm:ss", CultureInfo.CurrentCulture);
            var header = string.IsNullOrWhiteSpace(backup.Id)
                ? timestamp
                : $"{timestamp} — {backup.Id}";
            if (!string.IsNullOrWhiteSpace(backup.VintageStoryVersion))
                header += $" — VS {backup.VintageStoryVersion}";
            var item = new MenuItem
            {
                Header = header,
                Tag = backup
            };
            item.Click += RestoreDataFolderMenuItem_OnBackupClick;
            menuItem.Items.Add(item);
        }

        if (filteredBackups.Length > displayedBackups.Length)
        {
            menuItem.Items.Add(new MenuItem
            {
                Header = $"Showing latest {displayedBackups.Length} of {filteredBackups.Length} backups",
                IsEnabled = false
            });
        }
    }

    private async void RestoreDataFolderMenuItem_OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not DataFolderBackupSummary summary) return;

        if (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory))
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.DataDirectoryUnavailableForRestoreMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        var confirmed = await _confirmationService.ConfirmAsync(
                DataFolderBackupDialogTextBuilder.RestoreConfirmationMessage,
                "Simple VS Manager",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!confirmed) return;

        await RestoreDataBackupAsync(summary).ConfigureAwait(true);
    }

    private async void OpenDataBackupDirectoryMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var directory = _dataFolderBackupCoordinator.GetBackupRootDirectory();
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                await _confirmationService.NotifyAsync(
                        DataFolderBackupDialogTextBuilder.BackupDirectoryUnavailableMessage,
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                return;
            }

            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch (Win32Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.BuildOpenBackupDirectoryFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.BuildOpenBackupDirectoryFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async void ChangeBackupLocationMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var currentLocation = _dataFolderBackupCoordinator.GetBackupRootDirectory();

        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select a folder where backups will be saved and loaded from.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(currentLocation) && Directory.Exists(currentLocation))
        {
            dialog.InitialDirectory = currentLocation;
            dialog.SelectedPath = currentLocation;
        }

        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        var selectedPath = dialog.SelectedPath;
        if (string.IsNullOrWhiteSpace(selectedPath)) return;

        try
        {
            // Ensure the directory exists; creating it also validates write permissions
            Directory.CreateDirectory(selectedPath);

            _userConfiguration.SetCustomDataBackupLocation(selectedPath);
            _dataFolderBackupCoordinator.ChangeLocation(
                _userConfiguration.GetConfigurationDirectory(),
                _userConfiguration.CustomDataBackupLocation);

            _viewModel?.ReportStatus($"Backup location changed to: {selectedPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.BuildChangeBackupLocationFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async void DeleteDataFolderBackupsMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.SetDataDirectoryBeforeDeleteMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        var (installedVersion, normalizedInstalledVersion) = VintageStoryVersionLocator.GetNormalizedInstalledVersion(_gameDirectory);
        if (string.IsNullOrWhiteSpace(normalizedInstalledVersion))
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.UnknownInstalledVersionDeleteMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        var displayVersion = installedVersion ?? normalizedInstalledVersion;
        var confirmed = await _confirmationService.ConfirmAsync(
                DataFolderBackupDialogTextBuilder.BuildDeleteConfirmation(displayVersion),
                "Simple VS Manager",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!confirmed) return;

        try
        {
            var deleted = _dataFolderBackupCoordinator.DeleteBackups(_dataDirectory!, displayVersion);
            if (deleted == 0)
            {
                await _confirmationService.NotifyAsync(
                        DataFolderBackupDialogTextBuilder.NoMatchingBackupsMessage,
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);
                return;
            }

            var statusMessage = DataFolderBackupDialogTextBuilder.BuildDeletedStatusMessage(deleted);
            _viewModel?.ReportStatus(statusMessage);

            await _confirmationService.NotifyAsync(
                    statusMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                   or ArgumentException)
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.BuildDeleteFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async Task RestoreDataBackupAsync(DataFolderBackupSummary summary)
    {
        var check = _dataFolderBackupCoordinator.ValidateRestore(summary, _dataDirectory, _gameDirectory);

        switch (check.Result)
        {
            case DataBackupRestoreValidation.DataDirectoryUnavailable:
                await _confirmationService.NotifyAsync(
                        DataFolderBackupDialogTextBuilder.DataDirectoryUnavailableForRestoreMessage,
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                return;

            case DataBackupRestoreValidation.DifferentDataFolder:
                await _confirmationService.NotifyAsync(
                        DataFolderBackupDialogTextBuilder.DifferentDataFolderRestoreMessage,
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                return;

            case DataBackupRestoreValidation.VersionMismatch:
                await _confirmationService.NotifyAsync(
                        DataFolderBackupDialogTextBuilder.BuildVersionMismatchMessage(
                            check.BackupVersionDisplay!,
                            check.InstalledVersionDisplay!),
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                return;
        }

        ShowDataBackupOverlay("Preparing to restore VintagestoryData...");
        var progress = CreateDataBackupProgressReporter("Restoring VintagestoryData...");

        try
        {
            await _dataFolderBackupCoordinator.RestoreBackupAsync(summary, _dataDirectory!, progress, CancellationToken.None)
                .ConfigureAwait(true);
            await RefreshModsAsync(true).ConfigureAwait(true);
            _viewModel?.ReportStatus($"Restored VintagestoryData backup \"{summary.Id}\".");
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.RestoreSuccessMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await _confirmationService.NotifyAsync(
                    DataFolderBackupDialogTextBuilder.BuildRestoreFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        finally
        {
            HideDataBackupOverlay();
        }
    }

    private IProgress<DataFolderBackupProgress> CreateDataBackupProgressReporter(string fallbackMessage)
    {
        return new Progress<DataFolderBackupProgress>(value =>
        {
            var status = string.IsNullOrWhiteSpace(value.Status) ? fallbackMessage : value.Status;
            DataBackupStatusMessage = status;
            DataBackupProgress = double.IsNaN(value.Percent)
                ? 0
                : Math.Clamp(value.Percent, 0, 100);
        });
    }

    private void ShowDataBackupOverlay(string message)
    {
        DataBackupProgress = 0;
        DataBackupStatusMessage = message;
        IsDataBackupInProgress = true;
    }

    private void HideDataBackupOverlay()
    {
        IsDataBackupInProgress = false;
        DataBackupProgress = 0;
        DataBackupStatusMessage = string.Empty;
    }
}
