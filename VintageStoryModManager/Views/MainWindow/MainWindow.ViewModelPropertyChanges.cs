#nullable enable

using System.ComponentModel;
using System.Windows.Threading;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedSortOption))
        {
            if (_viewModel != null)
                Dispatcher.InvokeAsync(() =>
                {
                    if (_viewModel != null) UpdateSortPreferenceFromSelectedOption(!_suppressSortPreferenceSave);
                }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsCompactView))
        {
            if (_viewModel != null)
            {
                _userConfiguration.SetCompactViewMode(_viewModel.IsCompactView);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.UseModDbDesignView))
        {
            if (_viewModel != null) _userConfiguration.SetModDbDesignViewMode(_viewModel.UseModDbDesignView);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsViewingModlistTab))
        {
            if (_viewModel != null)
                Dispatcher.InvokeAsync(() =>
                {
                    if (_viewModel != null)
                    {
                        HandleModlistsVisibilityChanged(_viewModel.IsViewingModlistTab);
                        SyncMiddleTabControlToViewModel();
                    }
                }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsViewingMainTab))
        {
            if (_viewModel != null)
                Dispatcher.InvokeAsync(() =>
                {
                    if (_viewModel != null) SyncMiddleTabControlToViewModel();
                }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentModsView))
        {
            if (_viewModel != null)
                Dispatcher.InvokeAsync(() =>
                {
                    if (_viewModel != null)
                    {
                        var newView = _viewModel.CurrentModsView;
                        var previousView = _currentModsView;
                        var preserveState = ShouldPreserveModsViewState(previousView, newView);
                        AttachToModsView(newView, preserveState);
                    }
                }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsLoadingMods))
        {
            Dispatcher.InvokeAsync(() =>
            {
                RefreshHoverOverlayState();
                ScheduleRefreshAfterModlistLoadIfReady();
            }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsLoadingModDetails))
        {
            Dispatcher.InvokeAsync(() =>
            {
                RefreshHoverOverlayState();
                ScheduleRefreshAfterModlistLoadIfReady();
            }, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(MainViewModel.StatusMessage))
        {
            var statusMessage = _viewModel?.StatusMessage;
            if (ShouldRefreshAfterDependencyResolution(statusMessage))
                Dispatcher.InvokeAsync(
                    async () => { await RefreshModsAfterDependencyResolutionAsync().ConfigureAwait(true); },
                    DispatcherPriority.Background);

            Dispatcher.InvokeAsync(ScheduleRefreshAfterModlistLoadIfReady, DispatcherPriority.Background);
        }
    }
}
