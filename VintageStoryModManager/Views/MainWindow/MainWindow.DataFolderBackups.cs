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
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

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
            WpfMessageBox.Show(
                "The VintagestoryData folder is not available. Please set it before restoring a backup.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirmation = WpfMessageBox.Show(
            "Restoring a VintagestoryData backup replaces the entire folder (the Cache folder will be cleared). Continue?",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        await RestoreDataBackupAsync(summary).ConfigureAwait(true);
    }

    private void OpenDataBackupDirectoryMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var directory = _dataFolderBackupCoordinator.GetBackupRootDirectory();
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                WpfMessageBox.Show(
                    "The data backup directory is not available.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
            WpfMessageBox.Show(
                $"Failed to open the data backup directory:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            WpfMessageBox.Show(
                $"Failed to open the data backup directory:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ChangeBackupLocationMenuItem_OnClick(object? sender, RoutedEventArgs e)
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
            WpfMessageBox.Show(
                $"Failed to set the backup location:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DeleteDataFolderBackupsMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            WpfMessageBox.Show(
                "Set the VintagestoryData folder before deleting backups.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var (installedVersion, normalizedInstalledVersion) = VintageStoryVersionLocator.GetNormalizedInstalledVersion(_gameDirectory);
        if (string.IsNullOrWhiteSpace(normalizedInstalledVersion))
        {
            WpfMessageBox.Show(
                "The installed Vintage Story version could not be determined, so backups cannot be deleted safely.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var displayVersion = installedVersion ?? normalizedInstalledVersion;
        var confirmation = WpfMessageBox.Show(
            $"Delete all VintagestoryData backups for Vintage Story {displayVersion}? This action cannot be undone.",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            var deleted = _dataFolderBackupCoordinator.DeleteBackups(_dataDirectory!, displayVersion);
            if (deleted == 0)
            {
                WpfMessageBox.Show(
                    "No backups matching the current data folder and Vintage Story version were found.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _viewModel?.ReportStatus(
                deleted == 1
                    ? "Deleted 1 VintagestoryData backup."
                    : $"Deleted {deleted} VintagestoryData backups.");

            WpfMessageBox.Show(
                deleted == 1
                    ? "Deleted 1 VintagestoryData backup."
                    : $"Deleted {deleted} VintagestoryData backups.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                   or ArgumentException)
        {
            WpfMessageBox.Show(
                $"Failed to delete the VintagestoryData backups:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task RestoreDataBackupAsync(DataFolderBackupSummary summary)
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory)) return;

        if (!PathRelationshipHelper.IsSameDirectory(summary.SourceDataDirectory, _dataDirectory))
        {
            WpfMessageBox.Show(
                "This backup was created for a different VintagestoryData folder and cannot be restored.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var (installedVersion, normalizedInstalledVersion) = VintageStoryVersionLocator.GetNormalizedInstalledVersion(_gameDirectory);
        var normalizedBackupVersion = VersionStringUtility.Normalize(summary.VintageStoryVersion);
        if (!string.IsNullOrWhiteSpace(normalizedBackupVersion)
            && !string.IsNullOrWhiteSpace(normalizedInstalledVersion)
            && !string.Equals(normalizedBackupVersion, normalizedInstalledVersion, StringComparison.OrdinalIgnoreCase))
        {
            var backupVersionDisplay = summary.VintageStoryVersion ?? normalizedBackupVersion;
            var installedVersionDisplay = installedVersion ?? normalizedInstalledVersion;
            WpfMessageBox.Show(
                $"This backup was created for Vintage Story {backupVersionDisplay}, but the installed version is {installedVersionDisplay}. Install the matching Vintage Story version before restoring this backup.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
            WpfMessageBox.Show(
                "VintagestoryData was restored from the selected backup.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            WpfMessageBox.Show(
                $"Failed to restore the selected VintagestoryData backup:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
