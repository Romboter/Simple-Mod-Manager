#nullable enable

using System.ComponentModel;
using System.IO;
using System.Windows;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        ApplyStoredModInfoPanelPosition();

        _userConfiguration.EnablePersistence();

        await MigrateLegacyRebuiltModlistsIfNeededAsync().ConfigureAwait(true);

        // Ensure firebase-auth.json is backed up if it exists and hasn't been backed up yet
        FirebaseAnonymousAuthenticator.EnsureStartupBackup(_userConfiguration);

        try
        {
            var migrationOutcome = await _cloudWorkflowCoordinator.MigrateLegacyFirebaseDataIfNeededAsync().ConfigureAwait(true);

            if (migrationOutcome == CloudMigrationOutcome.Migrated)
            {
                await _confirmationService.NotifyAsync(
                        "Due to bandwidth issues, the manager is changing to another database. Your modlists will be saved and moved to the new database. Each user's modlists will appear in the Online Modlists tab when they update. All compatibility votes have been reset. Thank you for using the manager and voting!",
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus($"Failed to migrate cloud modlists to the new Firebase project: {ex.Message}",
                true);
        }

        await CheckAndPromptMigrationAsync().ConfigureAwait(true);

        await PromptCacheRefreshIfNeededAsync().ConfigureAwait(true);

        if (_viewModel != null)
        {
            await InitializeViewModelAsync(_viewModel).ConfigureAwait(true);
            await EnsureInstalledModsCachedAsync(_viewModel).ConfigureAwait(true);
            await CreateAppStartedBackupAsync().ConfigureAwait(true);

            // Sync installed mods to ModBrowser after ViewModel is initialized
            SyncInstalledModsToModBrowser();
        }

        await RefreshDeleteCachedModsMenuHeaderAsync();
        await RefreshManagerUpdateLinkAsync();
    }

    private async Task MigrateLegacyRebuiltModlistsIfNeededAsync()
    {
        if (_userConfiguration.RebuiltModlistMigrationCompleted) return;

        try
        {
            var modListDirectory = EnsureModListDirectory();
            var rebuiltDirectory = Path.Combine(modListDirectory, RebuiltModListDirectoryName);
            Directory.CreateDirectory(rebuiltDirectory);

            var movedAny = false;
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                         modListDirectory,
                         "Rebuilt_*",
                         SearchOption.TopDirectoryOnly))
            {
                if (Directory.Exists(entry))
                {
                    var targetPath = Path.Combine(rebuiltDirectory, Path.GetFileName(entry));
                    targetPath = FileNameHelper.EnsureUniqueDirectoryPath(targetPath);
                    Directory.Move(entry, targetPath);
                    movedAny = true;
                }
                else if (File.Exists(entry))
                {
                    var targetPath = Path.Combine(rebuiltDirectory, Path.GetFileName(entry));
                    targetPath = FileNameHelper.EnsureUniqueFilePath(targetPath);
                    File.Move(entry, targetPath);
                    movedAny = true;
                }
            }

            if (movedAny)
                _viewModel?.ReportStatus($"Moved rebuilt modlists into \"{RebuiltModListDirectoryName}\" folder.");

            _userConfiguration.SetRebuiltModlistMigrationCompleted();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            _modActivityLoggingService.LogError("Failed to prepare the Rebuilt modlists folder", ex);
            await _confirmationService.NotifyAsync(
                    $"Failed to prepare the Rebuilt modlists folder:\n{ex.Message}",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
    }

    private async Task PromptCacheRefreshIfNeededAsync()
    {
        if (!_userConfiguration.HasVersionMismatch || _userConfiguration.SuppressRefreshCachePrompt) return;

        var currentVersion = _userConfiguration.ModManagerVersion;
        var previousVersion = _userConfiguration.PreviousModManagerVersion
                              ?? _userConfiguration.PreviousConfigurationVersion;

        var message = previousVersion is null
            ? $"Simple VS Manager {currentVersion} is now installed. Clearing cached mod data is recommended after updates to avoid stale information.\n\nWould you like to clear the caches now?"
            : $"Simple VS Manager was updated from version {previousVersion} to {currentVersion}. Clearing cached mod data is recommended after updates to avoid stale information.\n\nWould you like to clear the caches now?";

        var result = await _confirmationService.ConfirmAsync(
                message,
                "Simple VS Manager",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (result) await ClearManagerCachesForVersionUpdateAsync().ConfigureAwait(true);
    }

    private async Task ClearManagerCachesForVersionUpdateAsync()
    {
        try
        {
            await Task.Run(() => ManagerCacheCleanupService.ClearManagerCaches(false)).ConfigureAwait(true);
            await RefreshDeleteCachedModsMenuHeaderAsync().ConfigureAwait(true);

            await _confirmationService.NotifyAsync(
                    "Cached mod data cleared successfully. Fresh data will be downloaded as needed.",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _modActivityLoggingService.LogError("Failed to clear cached mod data", ex);
            await _confirmationService.NotifyAsync(
                    $"Failed to clear cached mod data:\n{ex.Message}",
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private Task EnsureInstalledModsCachedAsync(MainViewModel viewModel, bool ignoreUserSetting = false)
    {
        if (viewModel is null || (!_userConfiguration.CacheAllVersionsLocally && !ignoreUserSetting))
            return Task.CompletedTask;

        var installedMods = viewModel.GetInstalledModsSnapshot();
        if (installedMods.Count == 0) return Task.CompletedTask;

        return Task.Run(() =>
        {
            foreach (var mod in installedMods)
            {
                if (mod is null || !mod.IsInstalled) continue;

                ModCacheService.EnsureModCached(mod.ModId, mod.Version, mod.SourcePath, mod.SourceKind);
            }
        });
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isApplyingPreset || _viewModel?.IsLoadingMods == true)
        {
            const string message =
                "A modlist is still being applied. Exiting now may leave some mods missing or disabled. Do you want to exit anyway?";

            var result = WpfMessageBox.Show(
                message,
                "Simple VS Manager",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        SaveWindowDimensions();
        SaveUploaderName();

        // Log timing summary before app exit (only if error/diagnostic logging is enabled)
        if (_viewModel != null && _userConfiguration.LogErrorsAndExceptions)
        {
            var timingSummary = _viewModel.TimingService.GetTimingSummary();
            _modActivityLoggingService.LogModLoadingTimingSummary(timingSummary);
        }

        _modActivityLoggingService.LogAppExit();

        // Clean up trace listener
        if (_traceListener != null)
        {
            System.Diagnostics.Trace.Listeners.Remove(_traceListener);
            _traceListener.Dispose();
            _traceListener = null;
        }

        DisposeCurrentViewModel();
        InternetAccessManager.InternetAccessChanged -= InternetAccessManager_OnInternetAccessChanged;
        DeveloperProfileManager.CurrentProfileChanged -= DeveloperProfileManager_OnCurrentProfileChanged;
    }
}
