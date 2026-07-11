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
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.NoDownloadableReleasesMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var release = ModReleaseSelectionHelper.SelectReleaseForInstall(modViewModel);
        if (release is null)
        {
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.NoDownloadableReleasesMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        if (!ModInstallTargetPathHelper.TryGetInstallTargetPath(_dataDirectory, modViewModel, release,
                out var targetPath, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                await _confirmationService.NotifyAsync(
                        errorMessage!,
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);

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
                await _confirmationService.NotifyAsync(
                        ModOperationDialogTextBuilder.BuildInstallFailureMessage(
                            modViewModel.DisplayName,
                            message),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.BuildInstallFailureMessage(
                        modViewModel.DisplayName,
                        ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        finally
        {
            _isModUpdateInProgress = false;
            UpdateSelectedModButtons();
        }
    }
}
