#nullable enable

using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void InstallModButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isModUpdateInProgress) return;

        if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

        e.Handled = true;

        await InstallModCoreAsync(mod, logErrorOnException: false, postInstallSuccess: () =>
        {
            if (mod.IsSelected) RemoveFromSelection(mod);
            _viewModel?.RemoveSearchResult(mod);
        });
    }

    private async Task InstallModCoreAsync(
        ModListItemViewModel modViewModel,
        bool logErrorOnException,
        Action postInstallSuccess)
    {
        if (!modViewModel.HasDownloadableRelease)
        {
            WpfMessageBox.Show("No downloadable releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var release = ModReleaseSelectionHelper.SelectReleaseForInstall(modViewModel);
        if (release is null)
        {
            WpfMessageBox.Show("No downloadable releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!ModInstallTargetPathHelper.TryGetInstallTargetPath(_dataDirectory, modViewModel, release,
                out var targetPath, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                WpfMessageBox.Show(errorMessage!,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

            return;
        }

        await CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait(true);

        _isModUpdateInProgress = true;
        UpdateSelectedModButtons();

        try
        {
            var descriptor = new ModUpdateDescriptor(
                modViewModel.ModId,
                modViewModel.DisplayName,
                release.DownloadUri,
                targetPath,
                false,
                release.FileName,
                release.Version,
                modViewModel.Version);

            var progress = new Progress<ModUpdateProgress>(p =>
                _viewModel?.ReportStatus($"{modViewModel.DisplayName}: {p.Message}"));

            var outcome = await ModUpdateOperationHelper.ExecuteAsync(
                    _modUpdateService, descriptor, _userConfiguration.CacheAllVersionsLocally, progress,
                    "The installation failed.")
                .ConfigureAwait(true);

            if (!outcome.Success)
            {
                var message = outcome.ErrorMessage!;
                _viewModel?.ReportStatus($"Failed to install {modViewModel.DisplayName}: {message}", true);
                WpfMessageBox.Show($"Failed to install {modViewModel.DisplayName}:{Environment.NewLine}{message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var versionText = string.IsNullOrWhiteSpace(release.Version) ? string.Empty : $" {release.Version}";
            _viewModel?.ReportStatus($"Installed {modViewModel.DisplayName}{versionText}.");
            _modActivityLoggingService.LogModInstall(modViewModel.DisplayName ?? modViewModel.ModId ?? "Unknown", release.Version);

            await RefreshModsAsync().ConfigureAwait(true);

            postInstallSuccess();
        }
        catch (OperationCanceledException)
        {
            _viewModel?.ReportStatus("Installation cancelled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (logErrorOnException)
                _modActivityLoggingService.LogError($"Failed to install {modViewModel.DisplayName}", ex);

            _viewModel?.ReportStatus($"Failed to install {modViewModel.DisplayName}: {ex.Message}", true);
            WpfMessageBox.Show($"Failed to install {modViewModel.DisplayName}:{Environment.NewLine}{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isModUpdateInProgress = false;
            UpdateSelectedModButtons();
        }
    }
}
