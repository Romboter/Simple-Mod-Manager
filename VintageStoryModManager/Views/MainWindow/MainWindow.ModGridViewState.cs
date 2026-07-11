#nullable enable

using System.Collections.Specialized;
using System.ComponentModel;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private bool ShouldPreserveModsViewState(ICollectionView? previousView, ICollectionView? nextView)
        {
            if (_viewModel is null || previousView is null || nextView is null) return false;

            var previousIsInstalled = ReferenceEquals(previousView, _viewModel.ModsView);
            var previousIsModDb = ReferenceEquals(previousView, _viewModel.SearchResultsView);
            var nextIsInstalled = ReferenceEquals(nextView, _viewModel.ModsView);

            // When leaving the Installed mods tab, always clear selection
            if (previousIsInstalled) return false;

            // When coming back to Installed mods from ModDB, preserve selection
            return previousIsModDb && nextIsInstalled;
        }

    private void AttachToModsView(ICollectionView? modsView, bool preserveState = false)
        {
            if (modsView is null)
            {
                if (_modsCollection != null)
                {
                    _modsCollection.CollectionChanged -= ModsView_OnCollectionChanged;
                    _modsCollection = null;
                }

                if (!preserveState) ClearSelection(true);

                _currentModsView = null;
                return;
            }

            if (_modsCollection != null)
            {
                _modsCollection.CollectionChanged -= ModsView_OnCollectionChanged;
                _modsCollection = null;
            }

            if (modsView is INotifyCollectionChanged notify)
            {
                _modsCollection = notify;
                notify.CollectionChanged += ModsView_OnCollectionChanged;
            }

            if (!preserveState) ClearSelection(true);

            _currentModsView = modsView;
        }

    private void ModsView_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, _viewModel?.ModsView)) return;

            // Don't clear selection during mod loading or mod details loading to allow user interaction
            if (_viewModel?.IsLoadingMods == true || _viewModel?.IsLoadingModDetails == true) return;

            Dispatcher.Invoke(() => ClearSelection(true));
        }
}
