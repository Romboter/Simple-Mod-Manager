#nullable enable
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void RestoreBackupMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        menuItem.Items.Clear();

        string directory;
        try
        {
            directory = EnsureBackupDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to access backup directory: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Backups unavailable",
                IsEnabled = false
            });
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to enumerate backups: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Backups unavailable",
                IsEnabled = false
            });
            return;
        }

        if (files.Length == 0)
        {
            menuItem.Items.Add(new MenuItem
            {
                Header = "No backups available",
                IsEnabled = false
            });
            return;
        }

        Array.Sort(files, (left, right) =>
            File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

        var appStartedAdded = false;

        foreach (var file in files)
        {
            var isAppStarted = BackupRetentionService.IsAppStartedBackup(file);
            if (isAppStarted)
            {
                if (appStartedAdded) continue;

                appStartedAdded = true;
            }

            var displayName = Path.GetFileNameWithoutExtension(file);
            var item = new MenuItem
            {
                Header = displayName,
                Tag = file
            };
            item.Click += RestoreBackupMenuItem_OnBackupClick;
            menuItem.Items.Add(item);
        }
    }

    private async void RestoreBackupMenuItem_OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string filePath) return;

        var confirmationDialog = new RestoreBackupDialog
        {
            Owner = this
        };

        var confirmation = confirmationDialog.ShowDialog();
        if (confirmation != true) return;

        await RestoreBackupAsync(filePath, confirmationDialog.RestoreConfigurations).ConfigureAwait(true);
    }

    private async Task RestoreBackupAsync(string backupPath, bool restoreConfigurations)
    {
        if (_viewModel is null) return;

        if (!File.Exists(backupPath))
        {
            await _confirmationService.NotifyAsync(
                    ModlistBackupDialogTextBuilder.SelectedBackupMissingMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        if (!PresetFileLoader.TryLoadPresetFromFile(backupPath,
                "Backup",
                ModListLoadOptions,
                out var preset,
                out var errorMessage))
        {
            var message = ModlistBackupDialogTextBuilder.BuildRestoreFailureMessage(errorMessage);
            await _confirmationService.NotifyAsync(
                    message,
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        var loadedPreset = preset!;
        await ApplyPresetAsync(loadedPreset, restoreConfigurations).ConfigureAwait(true);
        _viewModel.ReportStatus(
            ModlistBackupDialogTextBuilder.BuildRestoredStatusMessage(loadedPreset.Name));
    }

    private Task CreateAppStartedBackupAsync()
    {
        return CreateBackupAsync(
            "AppStarted",
            "Backup_AppStarted",
            false,
            true);
    }

    private Task CreateBackupAsync(
            string trigger,
            string fallbackFileName,
            bool pruneAutomaticBackups,
            bool pruneAppStartedBackups)
    {
        if (_viewModel is null) return Task.CompletedTask;

        var mods = _viewModel.GetInstalledModsSnapshot();
        var includedConfigurations = CaptureConfigurationsForBackup(mods);

        return _modlistBackupCoordinator.CreateBackupAsync(
            trigger,
            fallbackFileName,
            pruneAutomaticBackups,
            pruneAppStartedBackups,
            _viewModel.GetCurrentModStates(),
            mods.Count,
            includedConfigurations,
            ResolveGameVersion(null));
    }

    private IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? CaptureConfigurationsForBackup(
        IReadOnlyList<ModListItemViewModel> mods)
    {
        return ModConfigCaptureHelper.CaptureConfigurationsForMods(
            mods,
            _userConfiguration.GetModConfigPaths,
            _dataDirectory);
    }
}
