#nullable enable

using SimpleVsManager.Cloud;
using System.Net.Http;
using System.Threading;
using System.Windows;
using VintageStoryModManager.Services;
using WpfMessageBox =
    VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task ExecuteCloudOperationAsync(Func<FirebaseModlistStore, Task> operation, string actionDescription)
        {
            try
            {
                InternetAccessManager.ThrowIfInternetAccessDisabled();
            }
            catch (InternetAccessDisabledException ex)
            {
                WpfMessageBox.Show(ex.Message,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            FirebaseModlistStore store;
            try
            {
                store = await EnsureCloudStoreInitializedAsync();
            }
            catch (Exception ex)
            {
                StatusLogService.AppendStatus($"Failed to initialize cloud storage: {ex}", true);
                WpfMessageBox.Show($"Failed to initialize cloud storage:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                await operation(store);
                EnsureFirebaseAuthBackedUpIfAvailable();
            }
            catch (InternetAccessDisabledException ex)
            {
                WpfMessageBox.Show(ex.Message,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                StatusLogService.AppendStatus($"Network error while attempting to {actionDescription}: {ex.Message}", true);
                WpfMessageBox.Show($"Failed to {actionDescription}:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (InvalidOperationException ex)
            {
                StatusLogService.AppendStatus(
                    $"Cloud operation failed while attempting to {actionDescription}: {ex.Message}", true);
                WpfMessageBox.Show($"Failed to {actionDescription}:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusLogService.AppendStatus($"Unexpected error while attempting to {actionDescription}: {ex}", true);
                WpfMessageBox.Show($"An unexpected error occurred while attempting to {actionDescription}:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

    private async Task MigrateLegacyFirebaseDataIfNeededAsync()
        {
            if (_firebaseMigrationAttempted) return;

            var playerUid = _viewModel?.PlayerUid;
            if (string.IsNullOrWhiteSpace(playerUid)) return;

            _firebaseMigrationAttempted = true;

            try
            {
                var migrationService = new FirebaseModlistMigrationService();
                var migrationSucceeded = await migrationService
                    .TryMigrateAsync(playerUid, _viewModel?.PlayerName, _userConfiguration, CancellationToken.None)
                    .ConfigureAwait(true);

                if (migrationSucceeded)
                    await Dispatcher.InvokeAsync(() =>
                        WpfMessageBox.Show(
                            this,
                            "Due to bandwidth issues, the manager is changing to another database. Your modlists will be saved and moved to the new database. Each user's modlists will appear in the Online Modlists tab when they update. All compatibility votes have been reset. Thank you for using the manager and voting!",
                            "Simple VS Manager",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information));
            }
            catch (Exception ex)
            {
                StatusLogService.AppendStatus($"Failed to migrate cloud modlists to the new Firebase project: {ex.Message}",
                    true);
            }
        }

    private async Task<FirebaseModlistStore> EnsureCloudStoreInitializedAsync()
        {
            if (_cloudModlistStore is { } existingStore)
            {
                ApplyPlayerIdentityToCloudStore(existingStore);
                return existingStore;
            }

            await _cloudStoreLock.WaitAsync();
            try
            {
                if (_cloudModlistStore is { } cached)
                {
                    ApplyPlayerIdentityToCloudStore(cached);
                    return cached;
                }

                // Migration is only attempted once (see _firebaseMigrationAttempted flag).
                // The dialog is shown from the primary call in MainWindow_Loaded.
                await MigrateLegacyFirebaseDataIfNeededAsync().ConfigureAwait(false);

                var store = new FirebaseModlistStore();
                ApplyPlayerIdentityToCloudStore(store);
                _cloudModlistStore = store;
                return store;
            }
            finally
            {
                _cloudStoreLock.Release();
            }
        }

}
