#nullable enable
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using TabControl = System.Windows.Controls.TabControl;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void HandleModlistsVisibilityChanged(bool isVisible)
    {
        if (isVisible)
        {
            ApplyPreferredModlistsTabSelection();
            RefreshLocalModlists(false);

            // When opening modlists tab with Online sub-tab already selected, ensure cloud modlists load
            if (ModlistsTabControl is not null &&
                OnlineModlistsTabItem is not null &&
                Equals(ModlistsTabControl.SelectedItem, OnlineModlistsTabItem))
            {
                _ = RefreshCloudModlistsAsync(!_cloudModlistsLoaded);
            }

            return;
        }

        SetLocalModlistSelection(Array.Empty<LocalModlistListEntry>());
        if (LocalModlistsDataGrid is not null) LocalModlistsDataGrid.SelectedItems.Clear();
        SetCloudModlistSelection(null);
        if (CloudModlistsDataGrid != null) CloudModlistsDataGrid.SelectedItem = null;
    }

    private async void MiddleTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl) return;

        UpdateModBrowserTabVisibilityState(Equals(tabControl.SelectedItem, DatabaseTab));

        if (_isUpdatingMiddleTabSelection) return;
        if (_viewModel is null) return;

        if (Equals(tabControl.SelectedItem, MainTab))
        {
            if (_viewModel.ShowMainTabCommand?.CanExecute(null) == true)
                _viewModel.ShowMainTabCommand.Execute(null);
        }
        else if (Equals(tabControl.SelectedItem, DatabaseTab))
        {
            // Initialize the ModBrowserView when the Database tab is first selected
            if (ModBrowserView != null)
            {
                try
                {
                    await ModBrowserView.InitializeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MainWindow] Failed to initialize mod browser: {ex.Message}");
                }
            }

            if (_viewModel.ShowDatabaseTabCommand?.CanExecute(null) == true)
                _viewModel.ShowDatabaseTabCommand.Execute(null);
        }
        else if (Equals(tabControl.SelectedItem, ModlistTab))
        {
            if (_viewModel.ShowModlistTabCommand?.CanExecute(null) == true)
                _viewModel.ShowModlistTabCommand.Execute(null);
        }
    }

    private void UpdateModBrowserTabVisibilityState(bool isSelected)
    {
        if (_modBrowserViewModel == null) return;

        // The ModBrowser view can report IsVisible = false immediately after switching tabs,
        // which prevents new searches from running. Rely only on tab selection state here so
        // the view model always considers the tab visible while it is selected.
        var isVisible = isSelected && DatabaseTab?.IsVisible == true;
        _modBrowserViewModel.SetTabVisibility(isVisible);

        if (isVisible && ModBrowserView?.IsModBrowserInitialized == true)
        {
            _ = _modBrowserViewModel.RefreshSearchAsync();
        }
    }

    private void SyncMiddleTabControlToViewModel()
    {
        if (MiddleTabControl is null || _viewModel is null) return;

        TabItem? targetTab;

        if (_viewModel.IsViewingMainTab)
            targetTab = MainTab;
        else if (_viewModel.IsViewingModlistTab)
            targetTab = ModlistTab;
        else if (_viewModel.SearchModDatabase)
            targetTab = DatabaseTab;
        else
            targetTab = MainTab; // Default to MainTab if view section is unknown

        if (targetTab is null || Equals(MiddleTabControl.SelectedItem, targetTab)) return;

        _isUpdatingMiddleTabSelection = true;
        try
        {
            MiddleTabControl.SelectedItem = targetTab;
        }
        finally
        {
            _isUpdatingMiddleTabSelection = false;
        }
    }

    private async void ModlistsTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModlistsTabSelection) return;
        if (_viewModel?.IsViewingModlistTab != true) return;
        if (sender is not TabControl tabControl) return;
        if (OnlineModlistsTabItem is null || LocalModlistsTabItem is null) return;

        if (Equals(tabControl.SelectedItem, LocalModlistsTabItem))
        {
            _userConfiguration.SetPreferredModlistsTab(ModlistsTabSelection.Local);
            return;
        }

        if (!Equals(tabControl.SelectedItem, OnlineModlistsTabItem)) return;

        if (FirebaseAuthFileService.HasFirebaseAuthStateFile()) EnsureFirebaseAuthBackedUpIfAvailable();

        if (!await EnsureCloudModlistsConsentAsync().ConfigureAwait(true))
        {
            _isUpdatingModlistsTabSelection = true;
            try
            {
                tabControl.SelectedItem = LocalModlistsTabItem;
            }
            finally
            {
                _isUpdatingModlistsTabSelection = false;
            }

            _userConfiguration.SetPreferredModlistsTab(ModlistsTabSelection.Local);
            return;
        }

        _userConfiguration.SetPreferredModlistsTab(ModlistsTabSelection.Online);
        _ = RefreshCloudModlistsAsync(!_cloudModlistsLoaded);
    }

    private void ModlistsTabControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPreferredModlistsTabSelection();
    }

    private void ApplyPreferredModlistsTabSelection()
    {
        if (ModlistsTabControl is null || LocalModlistsTabItem is null || OnlineModlistsTabItem is null) return;

        var preferredTab = _userConfiguration.PreferredModlistsTab;
        var preferredItem = preferredTab == ModlistsTabSelection.Online
            ? OnlineModlistsTabItem
            : LocalModlistsTabItem;

        if (!Equals(ModlistsTabControl.SelectedItem, preferredItem)) ModlistsTabControl.SelectedItem = preferredItem;
    }
}
