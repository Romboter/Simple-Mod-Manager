#nullable enable

using System.Globalization;
using System.Text;
using System.Windows;
using VintageStoryModManager.Services;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void DeleteCachedModsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var result = WpfMessageBox.Show(
            "This will only delete the managers cached mods to save some disk space, it will not affect your installed mods.",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var deletionStatus = ManagerCacheCleanupService.DeleteCachedMods();

            switch (deletionStatus)
            {
                case CachedModsDeletionStatus.DirectoryUnavailable:
                    WpfMessageBox.Show(
                        "Could not determine the cached mods directory.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;

                case CachedModsDeletionStatus.DirectoryNotFound:
                    WpfMessageBox.Show(
                        "No cached mods were found.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                case CachedModsDeletionStatus.Deleted:
                    WpfMessageBox.Show(
                        "Cached mods deleted successfully.",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to delete cached mods:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        await RefreshDeleteCachedModsMenuHeaderAsync();
    }

    private void ClearAllCachesMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        const string confirmationMessage =
            "This will delete all cache folders used by Simple VS Manager:\n\n" +
            "• Temp Cache (contains all cache data)\n\n" +
            "Your settings, modlists and installed mods will NOT be affected.\n\n" +
            "This is useful when experiencing problems with the mod database or cached data.\n\n" +
            "Continue?";

        var confirmation = WpfMessageBox.Show(
            this,
            confirmationMessage,
            "Simple VS Manager",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.OK) return;

        try
        {
            var clearResult = ManagerCacheCleanupService.ClearAllManagerCacheFolders();

            if (!clearResult.DataDirectoryAvailable)
            {
                WpfMessageBox.Show(
                    this,
                    "Could not locate the Simple VS Manager data directory.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var messageBuilder = new StringBuilder();

            if (clearResult.DeletedFolders.Count > 0)
            {
                messageBuilder.AppendLine("Successfully deleted the following cache folders:");
                foreach (var folder in clearResult.DeletedFolders)
                    messageBuilder.AppendLine($"• {folder}");
            }
            else if (clearResult.FailedFolders.Count == 0)
            {
                messageBuilder.AppendLine("No cache folders were found to delete.");
            }

            if (clearResult.FailedFolders.Count > 0)
            {
                if (messageBuilder.Length > 0) messageBuilder.AppendLine();

                messageBuilder.AppendLine("Failed to delete the following cache folders:");
                foreach (var error in clearResult.FailedFolders)
                    messageBuilder.AppendLine($"• {error}");
            }

            WpfMessageBox.Show(
                this,
                messageBuilder.ToString(),
                "Simple VS Manager",
                MessageBoxButton.OK,
                clearResult.FailedFolders.Count > 0
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                this,
                $"An error occurred while clearing caches:\n\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ModsMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        await RefreshDeleteCachedModsMenuHeaderAsync();
    }

    private async Task RefreshDeleteCachedModsMenuHeaderAsync()
    {
        if (DeleteCachedModsMenuItem is null) return;

        const string baseHeader = "_Delete Cached Mods";
        var header = baseHeader;

        var cacheSize = await Task.Run(ManagerCacheCleanupService.GetCachedModsSize);
        if (cacheSize is long cacheSizeInBytes)
        {
            var cacheSizeInMegabytes =
                (long)Math.Round(cacheSizeInBytes / (1024d * 1024d), MidpointRounding.AwayFromZero);

            if (cacheSizeInMegabytes < 0) cacheSizeInMegabytes = 0;

            header = string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1}MB)",
                baseHeader,
                cacheSizeInMegabytes);
        }

        DeleteCachedModsMenuItem.Header = header;
    }
}
