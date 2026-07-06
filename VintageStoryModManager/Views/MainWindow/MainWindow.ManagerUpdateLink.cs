#nullable enable

using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ManagerUpdateLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        OpenManagerModDatabasePage();
    }

    private void ManagerModDbPageMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        OpenManagerModDatabasePage();
    }

    private void OpenManagerModDatabasePage()
    {
        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            WpfMessageBox.Show(
                "Internet access is disabled. Enable Internet Access in the File menu to open the mod database page.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            WpfMessageBox.Show(
                $"Failed to open the mod database page:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
