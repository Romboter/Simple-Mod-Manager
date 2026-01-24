using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;
using DataFolderBackupProgress = VintageStoryModManager.Services.DataBackupProgress;
using DataFolderBackupSummary = VintageStoryModManager.Services.DataBackupSummary;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for managing VintagestoryData backups.
/// Extracted from MainWindow.xaml.cs Phase 2.
/// </summary>
public partial class DataBackupViewModel : ObservableObject
{
    private readonly DataBackupService _dataBackupService;
    private readonly UserConfigurationService _userConfiguration;
    private readonly Window _owner;
    private readonly Func<string> _getDataDirectory;
    private readonly Func<string?> _getGameDirectory;

    [ObservableProperty]
    private bool _isDataBackupInProgress;

    [ObservableProperty]
    private double _dataBackupProgress;

    [ObservableProperty]
    private string _dataBackupStatusMessage = string.Empty;

    public DataBackupViewModel(
        DataBackupService dataBackupService,
        UserConfigurationService userConfiguration,
        Window owner,
        Func<string> getDataDirectory,
        Func<string?> getGameDirectory)
    {
        _dataBackupService = dataBackupService;
        _userConfiguration = userConfiguration;
        _owner = owner;
        _getDataDirectory = getDataDirectory;
        _getGameDirectory = getGameDirectory;
    }

    /// <summary>
    /// Gets available backups for the current data directory and game version.
    /// </summary>
    public IReadOnlyList<DataFolderBackupSummary> GetAvailableBackups()
    {
        return _dataBackupService.GetAvailableBackups();
    }

    /// <summary>
    /// Creates a backup of the VintagestoryData folder.
    /// Used before launching the game.
    /// </summary>
    public async Task<bool> TryEnsureDataBackupBeforeLaunchAsync()
    {
        if (!_userConfiguration.AutomaticDataBackupsEnabled) return true;

        var dataDirectory = _getDataDirectory();
        if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory)) return true;

        IsDataBackupInProgress = true;
        DataBackupProgress = 0;
        DataBackupStatusMessage = "Preparing VintagestoryData backup...";

        var installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion(_getGameDirectory());
        var progress = new Progress<DataFolderBackupProgress>(p =>
        {
            DataBackupProgress = p.Percent;
            DataBackupStatusMessage = p.Status;
        });

        try
        {
            await _dataBackupService
                .CreateBackupAsync(dataDirectory!, installedGameVersion, progress, CancellationToken.None)
                .ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var response = ModManagerMessageBox.Show(
                $"The automatic VintagestoryData backup failed:\n{ex.Message}\n\nLaunch Vintage Story without creating a backup?",
                "Simple VS Manager",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return response == MessageBoxResult.Yes;
        }
        catch (Exception ex)
        {
            ModManagerMessageBox.Show(
                $"The automatic VintagestoryData backup failed:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        finally
        {
            IsDataBackupInProgress = false;
            DataBackupProgress = 0;
            DataBackupStatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Restores a VintagestoryData backup.
    /// </summary>
    [RelayCommand]
    private async Task RestoreBackupAsync(DataFolderBackupSummary summary)
    {
        if (summary is null) return;

        var dataDirectory = _getDataDirectory();
        if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory))
        {
            ModManagerMessageBox.Show(
                "The VintagestoryData folder is not available. Please set it before restoring a backup.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirmation = ModManagerMessageBox.Show(
            "Restoring a VintagestoryData backup replaces the entire folder (the Cache folder will be cleared). Continue?",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        IsDataBackupInProgress = true;
        DataBackupProgress = 0;
        DataBackupStatusMessage = "Restoring backup...";

        var progress = new Progress<DataFolderBackupProgress>(p =>
        {
            DataBackupProgress = p.Percent;
            DataBackupStatusMessage = p.Status;
        });

        try
        {
            await _dataBackupService
                .RestoreBackupAsync(summary, dataDirectory!, progress, CancellationToken.None)
                .ConfigureAwait(true);

            DataBackupProgress = 100;
            DataBackupStatusMessage = "Backup restored successfully.";

            ModManagerMessageBox.Show(
                "The VintagestoryData backup was restored successfully.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Notify that data folder changed (will trigger mod list refresh)
            OnDataFolderRestored?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ModManagerMessageBox.Show(
                $"Failed to restore the backup:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsDataBackupInProgress = false;
            DataBackupProgress = 0;
            DataBackupStatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Opens the backup directory in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenBackupDirectory()
    {
        var directory = _dataBackupService.GetBackupRootDirectory();
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                ModManagerMessageBox.Show(
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
        catch (System.ComponentModel.Win32Exception ex)
        {
            ModManagerMessageBox.Show(
                $"Failed to open the data backup directory:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ModManagerMessageBox.Show(
                $"Failed to open the data backup directory:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Changes the backup location.
    /// </summary>
    [RelayCommand]
    private void ChangeBackupLocation()
    {
        var currentLocation = _dataBackupService.GetBackupRootDirectory();

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
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

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var selectedPath = dialog.SelectedPath;
        if (string.IsNullOrWhiteSpace(selectedPath)) return;

        try
        {
            Directory.CreateDirectory(selectedPath);
            _userConfiguration.SetCustomDataBackupLocation(selectedPath);

            // Notify that backup location changed (caller will recreate service)
            OnBackupLocationChanged?.Invoke(selectedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ModManagerMessageBox.Show(
                $"Failed to set the backup location:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Deletes all backups for the current data directory and game version.
    /// </summary>
    [RelayCommand]
    private void DeleteBackups()
    {
        var dataDirectory = _getDataDirectory();
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            ModManagerMessageBox.Show(
                "Set the VintagestoryData folder before deleting backups.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var installedVersion = VintageStoryVersionLocator.GetInstalledVersion(_getGameDirectory());
        var normalizedInstalledVersion = VersionStringUtility.Normalize(installedVersion);
        if (string.IsNullOrWhiteSpace(normalizedInstalledVersion))
        {
            ModManagerMessageBox.Show(
                "The installed Vintage Story version could not be determined, so backups cannot be deleted safely.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var displayVersion = installedVersion ?? normalizedInstalledVersion;
        var confirmation = ModManagerMessageBox.Show(
            $"Delete all VintagestoryData backups for Vintage Story {displayVersion}? This action cannot be undone.",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            var deleted = _dataBackupService.DeleteBackups(dataDirectory!, displayVersion);
            if (deleted == 0)
            {
                ModManagerMessageBox.Show(
                    "No backups matching the current data folder and Vintage Story version were found.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                var plural = deleted == 1 ? "backup" : "backups";
                ModManagerMessageBox.Show(
                    $"Deleted {deleted} {plural}.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Notify that backups were deleted (caller will refresh menu)
                OnBackupsDeleted?.Invoke();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ModManagerMessageBox.Show(
                $"Failed to delete backups:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Event raised when a backup is restored.
    /// Consumers should refresh the mod list.
    /// </summary>
    public event Action? OnDataFolderRestored;

    /// <summary>
    /// Event raised when the backup location changes.
    /// Consumers should recreate the DataBackupService.
    /// </summary>
    public event Action<string>? OnBackupLocationChanged;

    /// <summary>
    /// Event raised when backups are deleted.
    /// Consumers should refresh the backup menu.
    /// </summary>
    public event Action? OnBackupsDeleted;
}
