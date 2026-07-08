#nullable enable

using System.Globalization;
using System.Windows;
using VintageStoryModManager.Services;


namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void DeleteCachedModsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var result = await _confirmationService.ConfirmAsync(
                ManagerCacheDialogTextBuilder.DeleteCachedModsConfirmationMessage,
                "Simple VS Manager",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!result) return;

        try
        {
            var deletionStatus = ManagerCacheCleanupService.DeleteCachedMods();

            switch (deletionStatus)
            {
                case CachedModsDeletionStatus.DirectoryUnavailable:
                    await _confirmationService.NotifyAsync(
                            ManagerCacheDialogTextBuilder.CachedModsDirectoryUnavailableMessage,
                            "Simple VS Manager",
                            DialogSeverity.Warning)
                        .ConfigureAwait(true);
                    break;

                case CachedModsDeletionStatus.DirectoryNotFound:
                    await _confirmationService.NotifyAsync(
                            ManagerCacheDialogTextBuilder.NoCachedModsFoundMessage,
                            "Simple VS Manager",
                            DialogSeverity.Information)
                        .ConfigureAwait(true);
                    break;

                case CachedModsDeletionStatus.Deleted:
                    await _confirmationService.NotifyAsync(
                            ManagerCacheDialogTextBuilder.CachedModsDeletedSuccessfullyMessage,
                            "Simple VS Manager",
                            DialogSeverity.Information)
                        .ConfigureAwait(true);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ManagerCacheDialogTextBuilder.BuildDeleteCachedModsFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }

        await RefreshDeleteCachedModsMenuHeaderAsync();
    }

    private async void ClearAllCachesMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var confirmation = await _confirmationService.ConfirmAsync(
                ManagerCacheDialogTextBuilder.ClearAllCachesConfirmationMessage,
                "Simple VS Manager",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (!confirmation) return;

        try
        {
            var clearResult = ManagerCacheCleanupService.ClearAllManagerCacheFolders();

            if (!clearResult.DataDirectoryAvailable)
            {
                await _confirmationService.NotifyAsync(
                        ManagerCacheDialogTextBuilder.ManagerDataDirectoryUnavailableMessage,
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
                return;
            }

            await _confirmationService.NotifyAsync(
                    ManagerCacheDialogTextBuilder.BuildClearAllCachesResultMessage(
                        clearResult.DeletedFolders,
                        clearResult.FailedFolders),
                    "Simple VS Manager",
                    clearResult.FailedFolders.Count > 0
                        ? DialogSeverity.Warning
                        : DialogSeverity.Information)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ManagerCacheDialogTextBuilder.BuildClearAllCachesFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
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
