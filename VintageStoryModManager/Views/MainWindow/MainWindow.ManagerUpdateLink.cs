#nullable enable

using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void ManagerUpdateLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        await OpenManagerModDatabasePageAsync().ConfigureAwait(true);
    }

    private async void ManagerModDbPageMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await OpenManagerModDatabasePageAsync().ConfigureAwait(true);
    }

    private async Task OpenManagerModDatabasePageAsync()
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            await _confirmationService.NotifyAsync(
                    "Internet access is disabled. Enable Internet Access in the File menu to open the mod database page.",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ManagerModDatabaseUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ManagerUpdateLinkDialogTextBuilder.BuildOpenModDatabasePageFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async Task RefreshManagerUpdateLinkAsync()
    {
        if (ManagerUpdateLinkTextBlock is null) return;

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            ManagerUpdateLinkTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        var currentVersion = ManagerVersionHelper.GetManagerInformationalVersion();
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            ManagerUpdateLinkTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            // Use relaxed mode for manager updates to allow more flexible compatibility
            var info = await _modDatabaseService
                .TryLoadDatabaseInfoAsync(ManagerModDatabaseModId, currentVersion, null)
                .ConfigureAwait(true);

            var hasUpdate = info?.LatestVersion is string latestVersion
                            && VersionStringUtility.IsCandidateVersionNewer(latestVersion, currentVersion);

            ManagerUpdateLinkTextBlock.Visibility = hasUpdate ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            ManagerUpdateLinkTextBlock.Visibility = Visibility.Collapsed;
        }
    }

}
