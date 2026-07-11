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

        IReadOnlyList<string> files;
        try
        {
            files = _modlistBackupCoordinator.ListBackupFiles();
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

        if (files.Count == 0)
        {
            menuItem.Items.Add(new MenuItem
            {
                Header = "No backups available",
                IsEnabled = false
            });
            return;
        }

        foreach (var file in files)
        {
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

        var result = _modlistBackupCoordinator.LoadBackupForRestore(backupPath);

        if (result.FileMissing)
        {
            await _confirmationService.NotifyAsync(
                    ModlistBackupDialogTextBuilder.SelectedBackupMissingMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        if (result.Preset is null)
        {
            var message = ModlistBackupDialogTextBuilder.BuildRestoreFailureMessage(result.ErrorMessage);
            await _confirmationService.NotifyAsync(
                    message,
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        await ApplyPresetAsync(result.Preset, restoreConfigurations).ConfigureAwait(true);
        _viewModel.ReportStatus(
            ModlistBackupDialogTextBuilder.BuildRestoredStatusMessage(result.Preset.Name));
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
