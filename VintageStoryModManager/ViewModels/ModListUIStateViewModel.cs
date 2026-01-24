using CommunityToolkit.Mvvm.ComponentModel;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for UI state flags in the mod list.
/// Extracted from MainWindow.xaml.cs Phase 4.
/// Consolidates 27+ boolean flags into a centralized, observable state manager.
/// </summary>
public partial class ModListUIStateViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasAppliedInitialModInfoPanelPosition;

    [ObservableProperty]
    private bool _isApplyingMultiToggle;

    [ObservableProperty]
    private bool _isApplyingPreset;

    [ObservableProperty]
    private bool _isAutomaticRefreshRunning;

    [ObservableProperty]
    private bool _isCloudModlistRefreshInProgress;

    [ObservableProperty]
    private bool _isDependencyResolutionRefreshPending;

    [ObservableProperty]
    private bool _isDraggingModInfoPanel;

    [ObservableProperty]
    private bool _isInitializing;

    [ObservableProperty]
    private bool _isUpdatingModlistsTabSelection;

    [ObservableProperty]
    private bool _isUpdatingMiddleTabSelection;

    [ObservableProperty]
    private bool _isModUpdateInProgress;

    [ObservableProperty]
    private bool _isModUsageDialogOpen;

    [ObservableProperty]
    private bool _isRefreshingAfterModlistLoad;

    [ObservableProperty]
    private bool _isWindowActive;

    [ObservableProperty]
    private bool _refreshAfterModlistLoadPending;

    [ObservableProperty]
    private bool _localModlistsLoaded;

    [ObservableProperty]
    private bool _cloudModlistsLoaded;

    [ObservableProperty]
    private bool _firebaseMigrationAttempted;

    [ObservableProperty]
    private bool _suppressSortPreferenceSave;

    [ObservableProperty]
    private bool _isModBrowserWatcherSubscribed;

    /// <summary>
    /// Returns true if any operation is in progress that should block UI interactions.
    /// </summary>
    public bool IsAnyOperationInProgress =>
        IsApplyingPreset ||
        IsModUpdateInProgress ||
        IsAutomaticRefreshRunning ||
        IsCloudModlistRefreshInProgress;

    /// <summary>
    /// Returns true if the UI is safe to interact with (no blocking operations).
    /// </summary>
    public bool CanInteract => !IsAnyOperationInProgress;
}
