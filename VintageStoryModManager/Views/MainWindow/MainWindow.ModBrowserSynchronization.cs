#nullable enable

using System.Windows.Threading;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void InitializeVotesCacheWatcher()
    {
        try
        {
            var cachePath = ModVersionVoteService.GetVoteCachePath();
            _votesCacheWatcher = new VotesCacheWatcher(cachePath);
            _votesCacheWatcher.CacheChanged += OnVotesCacheChanged;
            _votesCacheWatcher.EnsureWatcher();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MainWindow] Failed to initialize votes cache watcher: {ex.Message}");
        }
    }

    private void OnVotesCacheChanged(object? sender, EventArgs e)
    {
        if (_modBrowserViewModel == null) return;

        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_votesCacheWatcher?.TryConsumePendingChanges() == true)
                {
                    _modBrowserViewModel.InvalidateAllVisibleUserReports();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainWindow] Failed to refresh mod browser user reports after cache change: {ex.Message}");
            }
        }, DispatcherPriority.Background);
    }

    private void SubscribeModBrowserToDirectoryWatcher()
    {
        if (_isModBrowserWatcherSubscribed || _viewModel?.ModsWatcher == null || _modBrowserViewModel == null) return;

        _viewModel.ModsWatcher.ChangesDetected += ModsWatcherOnChangesDetected;
        _isModBrowserWatcherSubscribed = true;
    }

    private void UnsubscribeModBrowserFromDirectoryWatcher()
    {
        if (!_isModBrowserWatcherSubscribed || _viewModel?.ModsWatcher == null) return;

        _viewModel.ModsWatcher.ChangesDetected -= ModsWatcherOnChangesDetected;
        _isModBrowserWatcherSubscribed = false;
    }

    private void ModsWatcherOnChangesDetected(object? sender, EventArgs e)
    {
        if (_modBrowserViewModel == null) return;

        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await _modBrowserViewModel.RefreshSearchAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainWindow] Failed to refresh mod browser search after directory change: {ex.Message}");
            }
        }, DispatcherPriority.Background);
    }

    private void SyncInstalledModsToModBrowser()
    {
        if (_modBrowserViewModel == null || _viewModel == null) return;

        var installedMods = _viewModel.GetInstalledModsSnapshot();
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(installedMods.Select(mod => mod.ModId));

        _modBrowserViewModel.UpdateInstalledMods(installedModIds, numericInstalledModIds);
    }
}
