#nullable enable

using System;
using System.Windows;
using System.Windows.Threading;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void StartModsWatcher()
    {
        if (_viewModel is null) return;

        StopModsWatcher();

        _modsWatcherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _modsWatcherTimer.Tick += ModsWatcherTimerOnTick;
        _modsWatcherTimer.Start();
    }

    private void StopModsWatcher()
    {
        if (_modsWatcherTimer is null) return;

        _modsWatcherTimer.Stop();
        _modsWatcherTimer.Tick -= ModsWatcherTimerOnTick;
        _modsWatcherTimer = null;
        _isAutomaticRefreshRunning = false;
    }

    private async void ModsWatcherTimerOnTick(object? sender, EventArgs e)
    {
        if (_viewModel is null || _viewModel.IsBusy || _isInitializing || _isAutomaticRefreshRunning) return;

        var hasChanges = await _viewModel.CheckForModStateChangesAsync();
        if (!hasChanges) return;

        if (_viewModel.RefreshCommand == null) return;

        _isAutomaticRefreshRunning = true;
        try
        {
            await RefreshModsAsync();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to refresh mods automatically:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isAutomaticRefreshRunning = false;
        }
    }
}
