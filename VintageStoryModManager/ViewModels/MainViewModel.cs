using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Application = System.Windows.Application;
using Timer = System.Threading.Timer;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Main view model that coordinates mod discovery and activation.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const string TagsColumnName = "Tags";
    private const string UserReportsColumnName = "UserReports";
    private static readonly TimeSpan InstalledModsSearchDebounceMin = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan InstalledModsSearchDebounceMax = TimeSpan.FromMilliseconds(300);
    private const int LargeModListThreshold = 200;
    private const int VeryLargeModListThreshold = 500;
    private static readonly TimeSpan BusyStateReleaseDelay = TimeSpan.FromMilliseconds(600);
    private static readonly int MaxNewModsRecentMonths = DevConfig.MaxNewModsRecentMonths;
    private static readonly int InstalledModsIncrementalBatchSize = DevConfig.InstalledModsIncrementalBatchSize;

    // Database refresh delays for startup optimization
    private const int InitialRefreshDelayMs = 500;  // Delay before starting database refresh on initial load
    private const int IncrementalRefreshDelayMs = 300;  // Delay before refreshing after incremental updates

    private readonly RelayCommand _clearSearchCommand;
    private readonly ClientSettingsWatcher _clientSettingsWatcher;
    private readonly ModlistCollectionsViewModel _modlistCollections = new();
    private readonly UserConfigurationService _configuration;
    private readonly ModDatabaseService _databaseService;
    private readonly ModDiscoveryService _discoveryService;
    private readonly object _searchDebounceLock = new();
    private readonly ModListSubscriptionManager _subscriptionManager;
    private readonly TagCacheService _tagCache = new();
    private readonly TagFilterService _tagFilterService;
    private readonly ObservableCollection<TagFilterOptionViewModel> _installedTagFilters = new();
    private string[] _lastInstalledAvailableTags = Array.Empty<string>();
    private readonly Dictionary<string, ModEntry> _modEntriesBySourcePath = new(StringComparer.OrdinalIgnoreCase);

    private readonly BatchedObservableCollection<ModListItemViewModel> _mods = new();
    private readonly ModsStateFingerprintTracker _modsStateFingerprintTracker;
    private readonly ModDirectoryWatcher _modsWatcher;

    private readonly Dictionary<string, ModListItemViewModel> _modViewModelsBySourcePath =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ObservableCollection<ModListItemViewModel> _searchResults = new();
    // Tag filtering is now handled by _tagFilterService
    private readonly ClientSettingsStore _settingsStore;
    private readonly TabNavigationViewModel _tabNavigation;
    private readonly ObservableCollection<SortOption> _sortOptions;
    private readonly UserReportsCoordinator _userReportsCoordinator;
    private readonly ModUpdatePollingService _updatePollingService;
    private readonly ModLoadingTimingService _timingService = new();
    private readonly ModDatabaseInfoRefreshService _databaseInfoRefreshService;

    private List<string>? _cachedBasePaths;
    private int _activeMods;
    private bool _allowModDetailsRefresh = true;
    private readonly BusyStateTracker _busyStateTracker;
    private readonly ModDetailsProgressTracker _modDetailsProgressTracker;
    private readonly OfflineModDatabaseInfoBuilder _offlineInfoBuilder;
    private bool _isInitialLoad = true;
    private bool _disposed;
    private Timer? _searchDebounceTimer;
    private CancellationTokenSource? _pendingSearchCts;
    private bool _hasActiveBusyScope;
    private bool _hasMultipleSelectedMods;
    private bool _hasSelectedMods;
    private bool _hasSelectedTags;
    private bool _hasShownModDetailsLoadingStatus;
    private bool _isAutoRefreshDisabled;
    private bool _isBusy;
    private bool _isCompactView;
    private bool _isErrorStatus;
    private bool _isFastCheckInProgress;
    private bool _isInstalledTagRefreshPending;
    private bool _isLoadingModDetails;
    private bool _isLoadingMods;
    private bool _isModDetailsProgressVisible;
    private double _modDetailsProgress;
    private string _modDetailsStatusText = string.Empty;
    private double _loadingProgress;
    private string _loadingStatusText = string.Empty;
    private bool _isModDetailsRefreshForced;
    private bool _isModDetailsStatusActive;
    private bool _isModInfoExpanded = true;
    private bool _isTagsColumnVisible = true;
    private bool _useModDbDesignView;
    private string _searchText = string.Empty;
    private string[] _searchTokens = Array.Empty<string>();
    private ModListItemViewModel? _selectedMod;

    private SortOption? _selectedSortOption;
    private string _statusMessage = string.Empty;
    private bool _suppressInstalledTagFilterSelectionChanges;
    private int _totalMods;
    private int _updatableModsCount;

    public event EventHandler<ModUserReportChangedEventArgs>? UserReportVoteSubmitted;

    public MainViewModel(
        string dataDirectory,
        UserConfigurationService configuration,
        string? gameDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(configuration);

        DataDirectory = Path.GetFullPath(dataDirectory);
        _configuration = configuration;

        _settingsStore = new ClientSettingsStore(DataDirectory);
        PerformClientSettingsCleanupIfNeeded();
        _discoveryService = new ModDiscoveryService(_settingsStore);
        _databaseService = new ModDatabaseService();
        _tagFilterService = new TagFilterService(_tagCache);
        InstalledGameVersion = VintageStoryVersionLocator.GetInstalledVersion(gameDirectory);
        _modsWatcher = new ModDirectoryWatcher(_discoveryService);
        _modsStateFingerprintTracker = new ModsStateFingerprintTracker(
            () => _discoveryService.GetModsStateFingerprint(),
            () => _modsWatcher.IsWatching);
        _clientSettingsWatcher = new ClientSettingsWatcher(_settingsStore.SettingsPath);
        _busyStateTracker = new BusyStateTracker(BusyStateReleaseDelay);
        _busyStateTracker.BusyChanged += OnBusyStateTrackerBusyChanged;
        _modDetailsProgressTracker = new ModDetailsProgressTracker(
            BeginBusyScope,
            BuildModDetailsLoadingStatusMessage,
            () => _isModDetailsStatusActive,
            UpdateIsLoadingModDetails,
            (progress, text) =>
            {
                ModDetailsProgress = progress;
                ModDetailsStatusText = text;
            },
            () => SetStatus(BuildModDetailsLoadingStatusMessage(), false, true),
            () => SetStatus(BuildModDetailsReadyStatusMessage(), false));
        _subscriptionManager = new ModListSubscriptionManager(
            _mods,
            _searchResults,
            OnInstalledModPropertyChanged,
            OnSearchResultPropertyChanged,
            OnInstalledModAttached,
            OnSearchResultAttached,
            () =>
            {
                ScheduleInstalledTagFilterRefresh();
                UpdateActiveCount();
            });
        _userReportsCoordinator = new UserReportsCoordinator(
            Path.Combine(DataDirectory, "voteEtags.json"),
            BeginBusyScope,
            () => InstalledGameVersion,
            () => _subscriptionManager.InstalledSubscriptions,
            () => _subscriptionManager.SearchResultSubscriptions,
            () => _allowModDetailsRefresh);
        _userReportsCoordinator.UserReportVoteSubmitted += (_, args) => UserReportVoteSubmitted?.Invoke(this, args);
        _offlineInfoBuilder = new OfflineModDatabaseInfoBuilder(
            () => InstalledGameVersion,
            () => _configuration.RequireExactVsVersionMatch);
        _updatePollingService = new ModUpdatePollingService(
            ModUpdatePollingService.DefaultInterval,
            () => _isAutoRefreshDisabled,
            ct => InvokeOnDispatcherAsync(
                () =>
                {
                    var snapshot = new List<ModEntry>(_modEntriesBySourcePath.Count);
                    foreach (var entry in _modEntriesBySourcePath.Values)
                    {
                        if (entry is null || string.IsNullOrWhiteSpace(entry.ModId)) continue;

                        snapshot.Add(entry);
                    }

                    return snapshot;
                },
                ct),
            (modId, ct) => _databaseService.TryFetchLatestReleaseVersionAsync(modId, ct),
            entries => QueueDatabaseInfoRefresh(entries, true),
            inProgress => IsFastCheckInProgress = inProgress);
        _databaseInfoRefreshService = new ModDatabaseInfoRefreshService(
            _databaseService,
            _offlineInfoBuilder,
            _timingService,
            () => InstalledGameVersion,
            () => _configuration.RequireExactVsVersionMatch,
            () => _allowModDetailsRefresh,
            () => _isInitialLoad,
            () => _isTagsColumnVisible,
            () => _userReportsCoordinator.IsVisible,
            count => OnModDetailsRefreshEnqueued(count),
            () => OnModDetailsRefreshCompleted(),
            ApplyDatabaseInfoBatchAsync,
            ApplyDatabaseInfoImmediateAsync);

        ModsView = CollectionViewSource.GetDefaultView(_mods);
        ModsView.Filter = FilterMod;
        SearchResultsView = CollectionViewSource.GetDefaultView(_searchResults);
        CloudModlistsView = _modlistCollections.CloudModlistsView;
        LocalModlistsView = _modlistCollections.LocalModlistsView;
        _modlistCollections.PropertyChanged += OnModlistCollectionsPropertyChanged;
        InstalledTagFilters = new ReadOnlyObservableCollection<TagFilterOptionViewModel>(_installedTagFilters);
        _sortOptions = new ObservableCollection<SortOption>(CreateSortOptions());
        SortOptions = new ReadOnlyObservableCollection<SortOption>(_sortOptions);
        SelectedSortOption = SortOptions.FirstOrDefault();
        SelectedSortOption?.Apply(ModsView);
        _isAutoRefreshDisabled = configuration.DisableAutoRefresh;
        _allowModDetailsRefresh = !_isAutoRefreshDisabled;

        _clearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => HasSearchText);
        ClearSearchCommand = _clearSearchCommand;

        _tabNavigation = new TabNavigationViewModel(
            ModsView,
            SearchResultsView,
            CloudModlistsView,
            () => InternetAccessManager.IsInternetAccessDisabled,
            message => SetStatus(message, false),
            OnTabSectionChanged);
        ShowMainTabCommand = _tabNavigation.ShowMainTabCommand;
        ShowDatabaseTabCommand = _tabNavigation.ShowDatabaseTabCommand;
        ShowModlistTabCommand = _tabNavigation.ShowModlistTabCommand;

        RefreshCommand = new AsyncRelayCommand(LoadModsAsync);
        SetStatus("Ready.", false);

        InternetAccessManager.InternetAccessChanged += OnInternetAccessChanged;

        _updatePollingService.ResetTimer();
    }

    public string DataDirectory { get; }

    public string? PlayerUid => _settingsStore.PlayerUid;

    public string? PlayerName => _settingsStore.PlayerName;

    public ICollectionView ModsView { get; }

    public ICollectionView SearchResultsView { get; }

    public ICollectionView CloudModlistsView { get; }

    public ICollectionView LocalModlistsView { get; }

    public ModDirectoryWatcher ModsWatcher => _modsWatcher;

    public ModLoadingTimingService TimingService => _timingService;

    public ReadOnlyObservableCollection<TagFilterOptionViewModel> InstalledTagFilters { get; }

    public ICollectionView CurrentModsView => _tabNavigation.CurrentModsView;

    public bool CanAccessCloudModlists => !InternetAccessManager.IsInternetAccessDisabled;

    public ReadOnlyObservableCollection<SortOption> SortOptions { get; }

    public SortOption? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value)) value?.Apply(ModsView);
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsLoadingMods
    {
        get => _isLoadingMods;
        private set
        {
            if (SetProperty(ref _isLoadingMods, value)) RecalculateIsBusy();
        }
    }

    public double LoadingProgress
    {
        get => _loadingProgress;
        private set => SetProperty(ref _loadingProgress, value);
    }

    public string LoadingStatusText
    {
        get => _loadingStatusText;
        private set => SetProperty(ref _loadingStatusText, value);
    }

    public bool IsLoadingModDetails
    {
        get => _isLoadingModDetails;
        private set
        {
            if (SetProperty(ref _isLoadingModDetails, value))
            {
                RecalculateIsBusy();
                UpdateModDetailsProgressVisibility();
            }
        }
    }

    public bool IsModDetailsProgressVisible
    {
        get => _isModDetailsProgressVisible;
        private set => SetProperty(ref _isModDetailsProgressVisible, value);
    }

    public bool IsFastCheckInProgress
    {
        get => _isFastCheckInProgress;
        private set
        {
            if (SetProperty(ref _isFastCheckInProgress, value)) UpdateModDetailsProgressVisibility();
        }
    }

    public double ModDetailsProgress
    {
        get => _modDetailsProgress;
        private set => SetProperty(ref _modDetailsProgress, value);
    }

    public string ModDetailsStatusText
    {
        get => _modDetailsStatusText;
        private set => SetProperty(ref _modDetailsStatusText, value);
    }

    public bool IsCompactView
    {
        get => _isCompactView;
        set => SetProperty(ref _isCompactView, value);
    }

    public bool HasSelectedTags
    {
        get => _hasSelectedTags;
        private set
        {
            if (SetProperty(ref _hasSelectedTags, value)) OnPropertyChanged(nameof(TagsColumnHeader));
        }
    }

    public string TagsColumnHeader => HasSelectedTags ? "Tags (*)" : "Tags";


    public bool IsModInfoExpanded
    {
        get => _isModInfoExpanded;
        set => SetProperty(ref _isModInfoExpanded, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ModListItemViewModel? SelectedMod
    {
        get => _selectedMod;
        private set
        {
            if (SetProperty(ref _selectedMod, value)) OnPropertyChanged(nameof(HasSelectedMod));
        }
    }

    public bool HasSelectedMod => SelectedMod != null;

    public bool HasSelectedMods
    {
        get => _hasSelectedMods;
        private set => SetProperty(ref _hasSelectedMods, value);
    }

    public bool HasMultipleSelectedMods
    {
        get => _hasMultipleSelectedMods;
        private set => SetProperty(ref _hasMultipleSelectedMods, value);
    }

    public bool IsErrorStatus
    {
        get => _isErrorStatus;
        private set => SetProperty(ref _isErrorStatus, value);
    }

    public IRelayCommand ShowMainTabCommand { get; }

    public IRelayCommand ShowDatabaseTabCommand { get; }

    public IRelayCommand ShowModlistTabCommand { get; }

    public bool IsViewingModlistTab => _tabNavigation.IsViewingModlistTab;

    public bool IsViewingMainTab => _tabNavigation.IsViewingMainTab;

    public bool SearchModDatabase => _tabNavigation.SearchModDatabase;

    public bool UseModDbDesignView
    {
        get => _useModDbDesignView;
        set => SetProperty(ref _useModDbDesignView, value);
    }

    public bool HasCloudModlists => _modlistCollections.HasCloudModlists;

    public bool HasLocalModlists => _modlistCollections.HasLocalModlists;

    public string SearchText
    {
        get => _searchText;
        set
        {
            var newValue = value ?? string.Empty;
            if (!SetProperty(ref _searchText, newValue)) return;

            var hadSearchTokens = _searchTokens.Length > 0;
            _searchTokens = CreateSearchTokens(newValue);
            var hasSearchTokens = _searchTokens.Length > 0;

            OnPropertyChanged(nameof(HasSearchText));
            _clearSearchCommand.NotifyCanExecuteChanged();

            // Only refresh if the search filter state actually changed.
            // This avoids unnecessary refreshes when clearing an already-empty search
            // or during tab switches where the search text is cleared.
            if (hadSearchTokens || hasSearchTokens)
                TriggerDebouncedInstalledModsSearch();

        }
    }

    public bool HasSearchText => _searchTokens.Length > 0;

    public int TotalMods
    {
        get => _totalMods;
        private set
        {
            if (SetProperty(ref _totalMods, value)) OnPropertyChanged(nameof(SummaryText));
        }
    }

    public int ActiveMods
    {
        get => _activeMods;
        private set
        {
            if (SetProperty(ref _activeMods, value)) OnPropertyChanged(nameof(SummaryText));
        }
    }

    public int UpdatableModsCount
    {
        get => _updatableModsCount;
        private set
        {
            if (SetProperty(ref _updatableModsCount, value))
            {
                OnPropertyChanged(nameof(UpdateAllButtonLabel));
                OnPropertyChanged(nameof(UpdateAllModsMenuHeader));
            }
        }
    }

    public string SummaryText => TotalMods == 0
        ? "No mods found."
        : $"{ActiveMods} active of {TotalMods} mods";

    public string UpdateAllButtonLabel => UpdatableModsCount == 0
        ? "Manage Updates"
        : $"Manage Updates ({UpdatableModsCount})";

    public string UpdateAllModsMenuHeader => UpdatableModsCount == 0
        ? "_Update All Mods"
        : $"_Update All Mods ({UpdatableModsCount})";

    public string NoModsFoundMessage =>
        $"No mods found. If this is unexpected, verify that your VintageStoryData folder is correctly set: {DataDirectory}. You can change it in the File Menu.";

    public IRelayCommand ClearSearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public string? InstalledGameVersion { get; }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        InternetAccessManager.InternetAccessChanged -= OnInternetAccessChanged;
        _subscriptionManager.Dispose();
        _modlistCollections.PropertyChanged -= OnModlistCollectionsPropertyChanged;
        _busyStateTracker.BusyChanged -= OnBusyStateTrackerBusyChanged;

        foreach (var filter in _installedTagFilters) filter.PropertyChanged -= OnInstalledTagFilterPropertyChanged;


        _installedTagFilters.Clear();

        _updatePollingService.Dispose();

        lock (_searchDebounceLock)
        {
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = null;
            _pendingSearchCts?.Cancel();
            _pendingSearchCts?.Dispose();
            _pendingSearchCts = null;
        }

        _databaseInfoRefreshService.Dispose();

        _clientSettingsWatcher.Dispose();
        _modDetailsProgressTracker.ReleaseBusyScope();
        _userReportsCoordinator.Dispose();
    }

    public IDisposable EnterBusyScope()
    {
        return BeginBusyScope();
    }

    public void SetInstalledColumnVisibility(string columnName, bool isVisible)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return;

        if (string.Equals(columnName, TagsColumnName, StringComparison.OrdinalIgnoreCase))
            SetTagsColumnVisibility(isVisible);
        else if (string.Equals(columnName, UserReportsColumnName, StringComparison.OrdinalIgnoreCase))
            SetUserReportsColumnVisibility(isVisible);
    }

    private void OnTabSectionChanged(ViewSection section)
    {
        if (!string.IsNullOrEmpty(_searchText)) SearchText = string.Empty;

        switch (section)
        {
            case ViewSection.DatabaseTab:
                SelectedMod = null;
                SetStatus("Showing mod database.", false);
                break;
            case ViewSection.MainTab:
                SelectedMod = null;
                SetStatus("Showing installed mods.", false);
                break;
            case ViewSection.ModlistTab:
                SelectedMod = null;
                SetStatus("Showing cloud modlists.", false);
                break;
        }

        // Notify critical property changes immediately
        OnPropertyChanged(nameof(IsViewingModlistTab));
        OnPropertyChanged(nameof(IsViewingMainTab));
        OnPropertyChanged(nameof(SearchModDatabase));
        OnPropertyChanged(nameof(CurrentModsView));

        // Defer non-critical property changes to avoid blocking UI thread during tab switch
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_tabNavigation.Current == ViewSection.MainTab) FastCheck();
        }, DispatcherPriority.Background);
    }

    public void FastCheck()
    {
        _updatePollingService.FastCheck();
    }

    public Task InitializeAsync()
    {
        return LoadModsAsync();
    }

    public void ReplaceCloudModlists(IEnumerable<CloudModlistListEntry>? entries)
    {
        _modlistCollections.ReplaceCloudModlists(entries);
    }

    public bool TryReplaceCloudModlist(CloudModlistListEntry existing, CloudModlistListEntry replacement)
    {
        return _modlistCollections.TryReplaceCloudModlist(existing, replacement);
    }

    public void ReplaceLocalModlists(IEnumerable<LocalModlistListEntry>? entries)
    {
        _modlistCollections.ReplaceLocalModlists(entries);
    }

    private void OnModlistCollectionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ModlistCollectionsViewModel.HasCloudModlists), StringComparison.Ordinal))
            OnPropertyChanged(nameof(HasCloudModlists));
        else if (string.Equals(e.PropertyName, nameof(ModlistCollectionsViewModel.HasLocalModlists), StringComparison.Ordinal))
            OnPropertyChanged(nameof(HasLocalModlists));
    }

    public IReadOnlyList<string> GetCurrentDisabledEntries()
    {
        return _settingsStore.GetDisabledEntriesSnapshot();
    }

    public IReadOnlyList<ModPresetModState> GetCurrentModStates()
    {
        return _mods
            .Select(mod => new ModPresetModState(mod.ModId, mod.Version, mod.IsActive, null, null))
            .ToList();
    }

    public IReadOnlyList<ModListItemViewModel> GetInstalledModsSnapshot()
    {
        return _mods.ToList();
    }

    public IReadOnlyList<ModUsageTrackingEntry> GetActiveModUsageSnapshot()
    {
        var result = new List<ModUsageTrackingEntry>();

        if (string.IsNullOrWhiteSpace(InstalledGameVersion)) return result;

        var gameVersion = InstalledGameVersion.Trim();
        var distinct = new HashSet<ModUsageTrackingKey>();

        foreach (var mod in _mods)
        {
            if (mod is null || !mod.IsActive) continue;

            if (string.IsNullOrWhiteSpace(mod.ModId) || string.IsNullOrWhiteSpace(mod.Version)) continue;

            var modId = mod.ModId.Trim();
            var modVersion = mod.Version.Trim();

            var key = new ModUsageTrackingKey(modId, modVersion, gameVersion);
            if (!distinct.Add(key)) continue;

            result.Add(new ModUsageTrackingEntry(
                modId,
                modVersion,
                gameVersion,
                mod.CanSubmitUserReport,
                mod.UserVoteOption.HasValue));
        }

        return result;
    }

    public IReadOnlyList<string> GetActiveModIdsSnapshot()
    {
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var mod in _mods)
        {
            if (mod is null || !mod.IsActive || string.IsNullOrWhiteSpace(mod.ModId)) continue;

            var trimmed = mod.ModId.Trim();
            if (trimmed.Length == 0) continue;

            if (distinct.Add(trimmed)) result.Add(trimmed);
        }

        return result;
    }

    public bool TryGetInstalledModDisplayName(string? modId, out string? displayName)
    {
        displayName = null;

        if (string.IsNullOrWhiteSpace(modId)) return false;

        foreach (var mod in _mods)
        {
            if (mod is null || string.IsNullOrWhiteSpace(mod.ModId)) continue;

            if (string.Equals(mod.ModId, modId, StringComparison.OrdinalIgnoreCase))
            {
                displayName = mod.DisplayName;
                return true;
            }
        }

        return false;
    }

    public async Task<bool> ApplyPresetAsync(ModPreset preset)
    {
        string? localError = null;

        bool success;
        if (preset.IncludesModStatus && preset.ModStates.Count > 0)
        {
            var installedMods = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in _mods)
            {
                if (mod is null || string.IsNullOrWhiteSpace(mod.ModId)) continue;

                var normalizedId = mod.ModId.Trim();
                if (!installedMods.ContainsKey(normalizedId))
                {
                    var version = string.IsNullOrWhiteSpace(mod.Version) ? null : mod.Version!.Trim();
                    installedMods.Add(normalizedId, version);
                }
            }

            success = await Task.Run(() =>
            {
                foreach (var state in preset.ModStates)
                {
                    if (state is null || string.IsNullOrWhiteSpace(state.ModId) ||
                        state.IsActive is not bool desiredState) continue;

                    var normalizedId = state.ModId.Trim();
                    var hasInstalledMod = installedMods.TryGetValue(normalizedId, out var installedVersion);

                    var recordedVersion = string.IsNullOrWhiteSpace(state.Version)
                        ? null
                        : state.Version!.Trim();

                    if (desiredState)
                    {
                        if (!hasInstalledMod) continue;

                        var versionsToActivate = new HashSet<string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            null
                        };

                        if (!string.IsNullOrWhiteSpace(installedVersion)) versionsToActivate.Add(installedVersion);

                        if (!string.IsNullOrWhiteSpace(recordedVersion)) versionsToActivate.Add(recordedVersion);

                        foreach (var versionKey in versionsToActivate)
                            if (!_settingsStore.TrySetActive(normalizedId, versionKey, true, out var error))
                            {
                                localError = error;
                                return false;
                            }
                    }
                    else
                    {
                        string? versionToDisable;

                        if (hasInstalledMod && !string.IsNullOrWhiteSpace(installedVersion))
                        {
                            versionToDisable = installedVersion;
                        }
                        else if (!string.IsNullOrWhiteSpace(recordedVersion))
                        {
                            versionToDisable = recordedVersion;
                        }
                        else
                        {
                            versionToDisable = null;
                        }

                        if (!_settingsStore.TrySetActive(normalizedId, versionToDisable, false, out var error))
                        {
                            localError = error;
                            return false;
                        }
                    }
                }

                localError = null;
                return true;
            });
        }
        else
        {
            var entries = preset.DisabledEntries ?? Array.Empty<string>();

            success = await Task.Run(() =>
            {
                var result = _settingsStore.TryApplyDisabledEntries(entries, out var error);
                localError = error;
                return result;
            });
        }

        if (!success)
        {
            var message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to apply preset \"{preset.Name}\"."
                : localError!;
            SetStatus(message, true);
            return false;
        }

        foreach (var mod in _mods)
        {
            var isDisabled = _settingsStore.IsDisabled(mod.ModId, mod.Version);
            mod.SetIsActiveSilently(!isDisabled);
        }

        UpdateActiveCount();
        SelectedSortOption?.Apply(ModsView);
        ModsView.Refresh();
        SetStatus($"Applied preset \"{preset.Name}\".", false);
        return true;
    }

    public void ReportStatus(string message, bool isError = false)
    {
        SetStatus(message, isError);
    }

    public void OnInternetAccessStateChanged()
    {

        if (_allowModDetailsRefresh && _modEntriesBySourcePath.Count > 0)
            QueueDatabaseInfoRefresh(_modEntriesBySourcePath.Values.ToArray());
    }

    internal void SetAutoRefreshDisabled(bool disabled)
    {
        _isAutoRefreshDisabled = disabled;
        _allowModDetailsRefresh = !_isAutoRefreshDisabled;

        if (disabled)
            _updatePollingService.StopTimer();
        else
            _updatePollingService.ResetTimer();
    }

    internal void ForceNextRefreshToLoadDetails()
    {
        _isModDetailsRefreshForced = true;
    }

    internal void RefreshInstalledModDetails()
    {
        if (_modEntriesBySourcePath.Count > 0)
            QueueDatabaseInfoRefresh(_modEntriesBySourcePath.Values.ToArray(), forceRefresh: true);
    }

    internal void SetSelectedMod(ModListItemViewModel? mod, int selectionCount)
    {
        HasSelectedMods = selectionCount > 0;
        HasMultipleSelectedMods = selectionCount > 1;
        SelectedMod = mod;
    }

    internal void RemoveSearchResult(ModListItemViewModel mod)
    {
        if (mod is null) return;

        _searchResults.Remove(mod);

        if (ReferenceEquals(SelectedMod, mod)) SelectedMod = null;
    }

    public ModListItemViewModel? FindInstalledModById(string? modId)
    {
        if (string.IsNullOrWhiteSpace(modId)) return null;

        var trimmed = modId.Trim();

        foreach (var mod in _mods)
        {
            if (mod is null || string.IsNullOrWhiteSpace(mod.ModId)) continue;

            if (string.Equals(mod.ModId.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)) return mod;
        }

        return null;
    }

    internal ModListItemViewModel? FindModBySourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return null;

        return _modViewModelsBySourcePath.TryGetValue(sourcePath, out var viewModel)
            ? viewModel
            : null;
    }

    internal async Task<bool> PreserveActivationStateAsync(string modId, string? previousVersion, string? newVersion,
        bool wasActive)
    {
        string? localError = null;

        var success = await Task.Run(() =>
            _settingsStore.TryUpdateDisabledEntry(modId, previousVersion, newVersion, !wasActive, out localError));

        if (!success)
        {
            var message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to preserve the activation state for {modId}."
                : localError!;
            SetStatus(message, true);
        }

        return success;
    }

    internal async Task<ActivationResult> ApplyActivationChangeAsync(ModListItemViewModel mod, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(mod);

        string? localError = null;
        var success = await Task.Run(() =>
        {
            var result = _settingsStore.TrySetActive(mod.ModId, mod.Version, isActive, out var error);
            localError = error;
            return result;
        });

        if (!success)
        {
            var message = string.IsNullOrWhiteSpace(localError)
                ? $"Failed to update {mod.DisplayName}."
                : localError!;
            SetStatus(message, true);
            return new ActivationResult(false, message);
        }

        UpdateActiveCount();
        ReapplyActiveSortIfNeeded();
        SetStatus(isActive ? $"Activated {mod.DisplayName}." : $"Deactivated {mod.DisplayName}.", false);
        return new ActivationResult(true, null);
    }

    internal IReadOnlyCollection<string> GetSourcePathsForModsWithErrors()
    {
        if (_mods.Count == 0) return Array.Empty<string>();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in _mods)
        {
            if (mod is null) continue;

            var sourcePath = mod.SourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath)) continue;

            if (mod.HasLoadError || mod.DependencyHasErrors || mod.MissingDependencies.Count > 0)
                result.Add(sourcePath);
        }

        return result.Count == 0 ? Array.Empty<string>() : result.ToArray();
    }

    internal async Task RefreshModsWithErrorsAsync(IReadOnlyCollection<string>? includeSourcePaths = null)
    {
        if (_mods.Count == 0 && _modEntriesBySourcePath.Count == 0) return;

        _modsWatcher.EnsureWatchers();
        var changeSet = _modsWatcher.ConsumeChanges();
        if (changeSet.RequiresFullRescan)
        {
            await LoadModsAsync().ConfigureAwait(true);
            return;
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includeSourcePaths is { Count: > 0 })
            foreach (var path in includeSourcePaths)
                if (!string.IsNullOrWhiteSpace(path))
                    candidates.Add(path);

        foreach (var path in changeSet.Paths)
            if (!string.IsNullOrWhiteSpace(path))
                candidates.Add(path);

        foreach (var mod in _mods)
        {
            if (mod is null || string.IsNullOrWhiteSpace(mod.SourcePath)) continue;

            if (mod.HasLoadError || mod.DependencyHasErrors || mod.MissingDependencies.Count > 0)
                candidates.Add(mod.SourcePath);
        }

        if (_modEntriesBySourcePath.Count > 0)
        {
            var allEntries = new List<ModEntry>(_modEntriesBySourcePath.Values);
            var recalculationSeed = new List<ModEntry>();

            foreach (var path in candidates)
                if (_modEntriesBySourcePath.TryGetValue(path, out var entry) && entry != null)
                    recalculationSeed.Add(entry);

            var impacted = recalculationSeed.Count == 0
                ? Array.Empty<ModEntry>()
                : await Task
                    .Run(() => _discoveryService.ApplyLoadStatusesIncremental(allEntries, recalculationSeed))
                    .ConfigureAwait(true);

            foreach (var entry in impacted)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.SourcePath)) continue;

                if (entry.HasLoadError
                    || entry.DependencyHasErrors
                    || (entry.MissingDependencies?.Count ?? 0) > 0)
                    candidates.Add(entry.SourcePath);
            }
        }

        if (candidates.Count == 0) return;

        var previousSelection = SelectedMod?.SourcePath;

        Dictionary<string, ModEntry> existingEntriesSnapshot =
            new(_modEntriesBySourcePath, StringComparer.OrdinalIgnoreCase);

        var reloadResults = await Task
            .Run(() => LoadChangedModEntries(candidates, existingEntriesSnapshot))
            .ConfigureAwait(true);

        var refreshedEntries = new List<ModEntry>(reloadResults.Count);
        var updatedEntriesForStatus = new List<ModEntry>(reloadResults.Count);
        HashSet<string>? removedModIds = null;

        foreach (var pair in reloadResults)
        {
            var path = pair.Key;
            var entry = pair.Value;

            if (entry == null)
            {
                _modEntriesBySourcePath.Remove(path);
                if (existingEntriesSnapshot.TryGetValue(path, out var previous))
                {
                    removedModIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(previous.ModId)) removedModIds.Add(previous.ModId);
                }

                continue;
            }

            _modEntriesBySourcePath[path] = entry;
            refreshedEntries.Add(entry);
            updatedEntriesForStatus.Add(entry);
        }

        IReadOnlyCollection<ModEntry> impactedEntries = Array.Empty<ModEntry>();

        if (_modEntriesBySourcePath.Count > 0)
        {
            var updatedEntriesSnapshot = new List<ModEntry>(_modEntriesBySourcePath.Values);
            impactedEntries = await Task
                .Run(() => _discoveryService.ApplyLoadStatusesIncremental(
                    updatedEntriesSnapshot,
                    updatedEntriesForStatus,
                    removedModIds))
                .ConfigureAwait(true);
        }

        ApplyPartialUpdates(reloadResults, previousSelection, impactedEntries);

        if (_allowModDetailsRefresh && refreshedEntries.Count > 0) QueueDatabaseInfoRefresh(refreshedEntries);

        TotalMods = _mods.Count;
        UpdateActiveCount();
        SelectedSortOption?.Apply(ModsView);
        await _modsStateFingerprintTracker.RefreshSnapshotAsync().ConfigureAwait(true);
    }

    private async Task LoadModsAsync()
    {
        if (IsLoadingMods) return;

        var forcedRefresh = _isModDetailsRefreshForced;
        _isModDetailsRefreshForced = false;
        var previousAllowDetails = _allowModDetailsRefresh;
        _allowModDetailsRefresh = !_isAutoRefreshDisabled || forcedRefresh;

        IsLoadingMods = true;
        LoadingProgress = 0;
        LoadingStatusText = string.Empty;
        using var busyScope = BeginBusyScope();
        SetStatus("Loading mods...", false);

        // Yield once so the UI thread has a chance to process the busy-state
        // notification before we start potentially expensive work below. This
        // keeps the refresh progress ring responsive instead of appearing to
        // freeze when the refresh begins.
        await Task.Yield();

        try
        {
            _modsWatcher.EnsureWatchers();
            var changeSet = _modsWatcher.ConsumeChanges();
            var requiresFullReload = _mods.Count == 0
                                     || changeSet.RequiresFullRescan
                                     || changeSet.Paths.Count == 0;

            var previousSelection = SelectedMod?.SourcePath;

            if (requiresFullReload)
            {
                await PerformFullReloadAsync(previousSelection).ConfigureAwait(true);
            }
            else
            {
                Dictionary<string, ModEntry> existingEntriesSnapshot =
                    new(_modEntriesBySourcePath, StringComparer.OrdinalIgnoreCase);
                var reloadResults =
                    await Task.Run(() => LoadChangedModEntries(changeSet.Paths, existingEntriesSnapshot));

                var updatedEntriesForStatus = new List<ModEntry>(reloadResults.Count);
                HashSet<string>? removedModIds = null;

                foreach (var pair in reloadResults)
                {
                    var path = pair.Key;
                    var entry = pair.Value;

                    if (entry == null)
                    {
                        _modEntriesBySourcePath.Remove(path);
                        if (existingEntriesSnapshot.TryGetValue(path, out var previous))
                        {
                            removedModIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            if (!string.IsNullOrWhiteSpace(previous.ModId)) removedModIds.Add(previous.ModId);
                        }
                    }
                    else
                    {
                        _modEntriesBySourcePath[path] = entry;
                        updatedEntriesForStatus.Add(entry);
                    }
                }

                var allEntries = new List<ModEntry>(_modEntriesBySourcePath.Values);
                var impacted = await Task
                    .Run(() => _discoveryService.ApplyLoadStatusesIncremental(allEntries, updatedEntriesForStatus,
                        removedModIds))
                    .ConfigureAwait(true);

                ApplyPartialUpdates(reloadResults, previousSelection, impacted);

                if (_allowModDetailsRefresh && updatedEntriesForStatus.Count > 0)
                {
                    // Defer database refresh to background during incremental updates
                    var entriesToRefresh = updatedEntriesForStatus.ToList();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(IncrementalRefreshDelayMs).ConfigureAwait(false);
                            QueueDatabaseInfoRefresh(entriesToRefresh);
                        }
                        catch (Exception ex)
                        {
                            // Log but don't crash - database refresh is not critical for app function
                            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Deferred incremental refresh failed: {ex.Message}");
                        }
                    });
                }
            }

            TotalMods = _mods.Count;
            UpdateActiveCount();
            SelectedSortOption?.Apply(ModsView);
            await _modsStateFingerprintTracker.RefreshSnapshotAsync();

            // Defer clearing IsLoadingMods until after any queued CollectionChanged events are processed.
            // This ensures the guard in ModsView_OnCollectionChanged works correctly during the critical
            // window when CollectionChanged events may be queued but not yet processed.
            await Application.Current.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load mods: {ex.Message}", true);
        }
        finally
        {
            IsLoadingMods = false;
            _allowModDetailsRefresh = previousAllowDetails;
        }
    }

    private async Task PerformFullReloadAsync(string? previousSelection)
    {
        // Copy previousEntries directly - no need for dispatcher since we're reading a dictionary
        // This optimization removes an unnecessary UI thread round-trip
        Dictionary<string, ModEntry> previousEntries = new(_modEntriesBySourcePath, StringComparer.OrdinalIgnoreCase);

        IProgress<LoadingProgressUpdate> progressReporter = new Progress<LoadingProgressUpdate>(update =>
        {
            LoadingProgress = update.Progress;
            LoadingStatusText = update.Status;
        });

        progressReporter.Report(new LoadingProgressUpdate(0, "Discovering mods..."));

        var batchSize = InstalledModsIncrementalBatchSize;

        var loadResult = await Task
            .Run<(List<ModEntry> entries, List<(string sourcePath, ModEntry entry, ModListItemViewModel viewModel)> viewModels)>(
                async () =>
            {
                var allEntries = new List<ModEntry>();
                var processedCount = 0;
                var batchesSinceYield = 0;
                const int yieldEveryNBatches = 5; // Yield every 5 batches instead of every batch to reduce context switching

                // Use incremental loading on a background thread to keep UI responsive
                await foreach (var batch in _discoveryService.LoadModsIncrementallyAsync(batchSize, CancellationToken.None))
                {
                    if (batch.Count == 0) continue;

                    foreach (var entry in batch)
                    {
                        ResetCalculatedModState(entry);
                        if (previousEntries.TryGetValue(entry.SourcePath, out var previous))
                            CopyTransientModState(previous, entry);
                        allEntries.Add(entry);
                    }

                    processedCount += batch.Count;
                    batchesSinceYield++;

                    // Update progress - we don't know total yet, so show a generic loading message
                    var discoveryProgress = Math.Min(45, 5 + processedCount * 0.1);
                    progressReporter.Report(
                        new LoadingProgressUpdate(discoveryProgress, $"Loading mods... ({processedCount} found)"));

                    // Yield less frequently to reduce context switch overhead
                    // Only yield every N batches to keep UI responsive without excessive overhead
                    if (batchesSinceYield >= yieldEveryNBatches)
                    {
                        batchesSinceYield = 0;
                        await Task.Yield();
                    }
                }

                if (allEntries.Count > 0)
                {
                    progressReporter.Report(new LoadingProgressUpdate(60, $"Processing {allEntries.Count} mods..."));
                    _discoveryService.ApplyLoadStatuses(allEntries);
                }

                progressReporter.Report(new LoadingProgressUpdate(80, $"Preparing {allEntries.Count} mods..."));

                // Pre-create view models off the UI thread to reduce dispatcher work
                // Note: CreateModViewModel is safe to call off UI thread because:
                // - _settingsStore.IsDisabled() is thread-safe (uses lock)
                // - GetDisplayPath() uses cached base paths (thread-safe read)
                // - ModListItemViewModel constructor doesn't require UI thread
                var pendingViewModels =
                    new List<(string sourcePath, ModEntry entry, ModListItemViewModel viewModel)>(allEntries.Count);
                foreach (var entry in allEntries)
                {
                    var viewModel = CreateModViewModel(entry);
                    pendingViewModels.Add((entry.SourcePath, entry, viewModel));
                }

                return (allEntries, pendingViewModels);
            })
            .ConfigureAwait(true);

        var entries = loadResult.entries;
        var viewModelEntries = loadResult.viewModels;

        progressReporter.Report(new LoadingProgressUpdate(90, $"Updating UI with {entries.Count} mods..."));

        await InvokeOnDispatcherAsync(() =>
        {
            _modEntriesBySourcePath.Clear();
            _modViewModelsBySourcePath.Clear();

            // Use batch operation to minimize UI notifications
            using (_mods.SuspendNotifications())
            {
                _mods.Clear();

                // Use pre-created view models to reduce work on UI thread
                foreach (var (sourcePath, entry, viewModel) in viewModelEntries)
                {
                    _modEntriesBySourcePath[sourcePath] = entry;
                    _modViewModelsBySourcePath[sourcePath] = viewModel;
                    _mods.Add(viewModel);
                }
            }

            TotalMods = _mods.Count;

            if (!string.IsNullOrWhiteSpace(previousSelection)
                && _modViewModelsBySourcePath.TryGetValue(previousSelection, out var selected))
                SelectedMod = selected;
            else
                SelectedMod = null;

            UpdateLoadedModsStatus();

            // Defer database refresh to background to show UI faster
            if (_allowModDetailsRefresh && entries.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Small delay to ensure UI is responsive first
                        await Task.Delay(InitialRefreshDelayMs).ConfigureAwait(false);
                        QueueDatabaseInfoRefresh(entries);

                        // Mark initial load as complete after first database refresh
                        _isInitialLoad = false;
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash - database refresh is not critical for app function
                        System.Diagnostics.Debug.WriteLine($"[MainViewModel] Deferred database refresh failed: {ex.Message}");
                        _isInitialLoad = false;
                    }
                });
            }
            else
            {
                // If no database refresh needed, mark as complete immediately
                _isInitialLoad = false;
            }
        }, CancellationToken.None).ConfigureAwait(true);

        // Complete
        progressReporter.Report(new LoadingProgressUpdate(100, $"Loaded {entries.Count} mods"));
    }

    private readonly record struct LoadingProgressUpdate(double Progress, string Status);

    public async Task<bool> CheckForModStateChangesAsync()
    {
        _clientSettingsWatcher.EnsureWatcher();
        var clientSettingsTriggeredRefresh = false;
        if (_clientSettingsWatcher.TryConsumePendingChanges())
        {
            var result = await ApplyClientSettingsChangesAsync().ConfigureAwait(true);
            if (!result.success)
                _clientSettingsWatcher.SignalPendingChange();
            else if (result.modStatesChanged) clientSettingsTriggeredRefresh = true;
        }

        _modsWatcher.EnsureWatchers();

        if (clientSettingsTriggeredRefresh) return true;

        if (_modsWatcher.HasPendingChanges) return true;

        if (_modsWatcher.IsWatching) return false;

        return await _modsStateFingerprintTracker.HasFingerprintChangedAsync().ConfigureAwait(false);
    }

    private async Task<(bool success, bool modStatesChanged)> ApplyClientSettingsChangesAsync()
    {
        string? localError = null;
        var reloadSuccess = await Task.Run(() => _settingsStore.TryReload(out localError)).ConfigureAwait(true);

        if (!reloadSuccess)
        {
            if (!string.IsNullOrWhiteSpace(localError))
                await InvokeOnDispatcherAsync(
                    () => SetStatus($"Failed to reload client settings: {localError}", true),
                    CancellationToken.None).ConfigureAwait(true);

            return (false, false);
        }

        // Invalidate cached base paths after settings reload since search paths may have changed
        _cachedBasePaths = null;

        var modStatesChanged = false;
        await InvokeOnDispatcherAsync(() =>
        {
            foreach (var mod in _mods)
            {
                var shouldBeActive = !_settingsStore.IsDisabled(mod.ModId, mod.Version);
                if (mod.IsActive != shouldBeActive)
                {
                    mod.SetIsActiveSilently(shouldBeActive);
                    modStatesChanged = true;
                }
            }

            if (modStatesChanged)
            {
                UpdateActiveCount();
                ReapplyActiveSortIfNeeded();
            }
        }, CancellationToken.None).ConfigureAwait(true);

        return (true, modStatesChanged);
    }

    private ModListItemViewModel CreateModViewModel(ModEntry entry)
    {
        var isActive = !_settingsStore.IsDisabled(entry.ModId, entry.Version);
        var location = GetDisplayPath(entry.SourcePath);
        return new ModListItemViewModel(
            entry,
            isActive,
            location,
            ApplyActivationChangeAsync,
            InstalledGameVersion,
            true,
            _configuration.ShouldSkipModVersion,
            () => _configuration.RequireExactVsVersionMatch,
            _allowModDetailsRefresh,
            _timingService);
    }

    private void UpdateActiveCount()
    {
        ActiveMods = _mods.Count(item => item.IsActive);
        UpdateUpdatableCount();
    }

    private void UpdateUpdatableCount()
    {
        UpdatableModsCount = _mods.Count(item => item.CanUpdate);
    }

    private void ClearSearchResults()
    {
        if (_searchResults.Count == 0)
        {
            SelectedMod = null;
            return;
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() =>
            {
                _searchResults.Clear();
                SelectedMod = null;
            });
            return;
        }

        _searchResults.Clear();
        SelectedMod = null;
    }

    // EnumerateModTags logic is now handled by TagFilterService.UpdateInstalledAvailableTagsFromMods

    private void OnInstalledModAttached(ModListItemViewModel mod)
    {
        if (_allowModDetailsRefresh) QueueUserReportRefresh(mod);
    }

    private void OnSearchResultAttached(ModListItemViewModel mod)
    {
        if (mod.CanSubmitUserReport) QueueUserReportRefresh(mod);

        QueueLatestReleaseUserReportRefresh(mod);
    }

    private void OnInstalledModPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ModListItemViewModel.DatabaseTags), StringComparison.Ordinal))
        {
            ScheduleInstalledTagFilterRefresh();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(ModListItemViewModel.IsActive), StringComparison.Ordinal))
        {
            UpdateActiveCount();
            return;
        }

        if (string.Equals(e.PropertyName, nameof(ModListItemViewModel.CanUpdate), StringComparison.Ordinal))
            UpdateUpdatableCount();
    }

    private void OnSearchResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ModListItemViewModel mod) return;

        if (string.Equals(e.PropertyName, nameof(ModListItemViewModel.DatabaseTags), StringComparison.Ordinal))
        {

            return;
        }

        if (string.Equals(e.PropertyName, nameof(ModListItemViewModel.UserReportModVersion), StringComparison.Ordinal))
            if (mod.CanSubmitUserReport)
                QueueUserReportRefresh(mod);
    }

    private void QueueLatestReleaseUserReportRefresh(ModListItemViewModel mod)
    {
        _userReportsCoordinator.QueueLatestReleaseUserReportRefresh(mod);
    }

    public Task<ModVersionVoteSummary?> RefreshLatestReleaseUserReportAsync(
        ModListItemViewModel mod,
        CancellationToken cancellationToken = default)
    {
        return _userReportsCoordinator.RefreshLatestReleaseUserReportAsync(mod, cancellationToken);
    }

    private void QueueUserReportRefresh(ModListItemViewModel mod)
    {
        _userReportsCoordinator.QueueUserReportRefresh(mod);
    }

    public void EnableUserReportFetching(bool includeInstalledWhenAutoRefreshDisabled = false)
    {
        _userReportsCoordinator.EnableUserReportFetching(includeInstalledWhenAutoRefreshDisabled);
    }

    public Task<ModVersionVoteSummary?> RefreshUserReportAsync(
        ModListItemViewModel mod,
        CancellationToken cancellationToken = default)
    {
        return _userReportsCoordinator.RefreshUserReportAsync(mod, cancellationToken);
    }

    public Task<ModVersionVoteSummary?> SubmitUserReportVoteAsync(
        ModListItemViewModel mod,
        ModVersionVoteOption? option,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        return _userReportsCoordinator.SubmitUserReportVoteAsync(mod, option, comment, cancellationToken);
    }

    private void SetTagsColumnVisibility(bool isVisible)
    {
        if (_isTagsColumnVisible == isVisible) return;

        _isTagsColumnVisible = isVisible;

        if (!isVisible)
        {
            foreach (var mod in _mods) mod.ClearDatabaseTags();

            foreach (var mod in _searchResults) mod.ClearDatabaseTags();

            return;
        }

        ScheduleInstalledTagFilterRefresh();
        if (_allowModDetailsRefresh && _modEntriesBySourcePath.Count > 0)
            QueueDatabaseInfoRefresh(_modEntriesBySourcePath.Values.ToArray());
    }

    private void SetUserReportsColumnVisibility(bool isVisible)
    {
        if (_userReportsCoordinator.SetVisibility(isVisible) && isVisible && _allowModDetailsRefresh)
            EnableUserReportFetching();
    }

    private void ScheduleInstalledTagFilterRefresh()
    {
        if (!_isTagsColumnVisible) return;

        if (_isInstalledTagRefreshPending) return;

        _isInstalledTagRefreshPending = true;

        async void ExecuteAsync()
        {
            try
            {
                // Update tag filter service on background thread
                await Task.Run(() =>
                {
                    _tagFilterService.UpdateInstalledAvailableTagsFromMods(_mods);
                }).ConfigureAwait(false);

                await InvokeOnDispatcherAsync(
                        () => ApplyInstalledTagFilters(_tagFilterService.GetInstalledAvailableTags()),
                        CancellationToken.None,
                        DispatcherPriority.ContextIdle)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Swallow unexpected exceptions for resilience.
            }
            finally
            {
                _isInstalledTagRefreshPending = false;
            }
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(ExecuteAsync));
        else
            ExecuteAsync();
    }

    private void ResetInstalledTagFilters(IEnumerable<string> tags)
    {
        if (!_isTagsColumnVisible) return;

        _tagFilterService.SetInstalledAvailableTags(tags.Concat(_tagFilterService.GetSelectedInstalledTags()));
        ApplyInstalledTagFilters(_tagFilterService.GetInstalledAvailableTags());
    }

    private void ApplyInstalledTagFilters(IReadOnlyList<string> normalized)
    {
        if (!_isTagsColumnVisible) return;

        var normalizedTags = normalized
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (_lastInstalledAvailableTags.SequenceEqual(normalizedTags, StringComparer.OrdinalIgnoreCase)) return;

        _lastInstalledAvailableTags = normalizedTags;

        _suppressInstalledTagFilterSelectionChanges = true;
        try
        {
            foreach (var filter in _installedTagFilters) filter.PropertyChanged -= OnInstalledTagFilterPropertyChanged;

            _installedTagFilters.Clear();

            foreach (var tag in normalizedTags)
            {
                var isSelected = _tagFilterService.IsInstalledTagSelected(tag);
                var option = new TagFilterOptionViewModel(tag, isSelected);
                option.PropertyChanged += OnInstalledTagFilterPropertyChanged;
                _installedTagFilters.Add(option);
            }
        }
        finally
        {
            _suppressInstalledTagFilterSelectionChanges = false;
        }

        SyncSelectedTagsToService(_installedTagFilters, isInstalled: true);
        UpdateHasSelectedTags();
        ModsView.Refresh();
    }

    private void OnInstalledTagFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressInstalledTagFilterSelectionChanges) return;

        if (!string.Equals(e.PropertyName, nameof(TagFilterOptionViewModel.IsSelected),
                StringComparison.Ordinal)) return;

        if (SyncSelectedTagsToService(_installedTagFilters, isInstalled: true))
        {
            UpdateHasSelectedTags();
            ModsView.Refresh();
        }
    }

    private bool SyncSelectedTagsToService(IEnumerable<TagFilterOptionViewModel> filters, bool isInstalled)
    {
        var newSelection = filters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToList();

        return _tagFilterService.SetSelectedInstalledTags(newSelection);
    }

    private void UpdateHasSelectedTags()
    {
        HasSelectedTags = _tagFilterService.HasSelectedTags;
    }

    private IDisposable BeginBusyScope()
    {
        return _busyStateTracker.BeginScope();
    }

    private void OnBusyStateTrackerBusyChanged(bool isBusy)
    {
        if (Application.Current?.Dispatcher is Dispatcher dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                _hasActiveBusyScope = isBusy;
                RecalculateIsBusy();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    _hasActiveBusyScope = isBusy;
                    RecalculateIsBusy();
                }));
            }
        }
        else
        {
            _hasActiveBusyScope = isBusy;
            RecalculateIsBusy();
        }
    }

    private void RecalculateIsBusy()
    {
        var isBusy = _hasActiveBusyScope || _isLoadingMods || _isLoadingModDetails;

        if (Application.Current?.Dispatcher is Dispatcher dispatcher)
        {
            if (dispatcher.CheckAccess())
                IsBusy = isBusy;
            else
                dispatcher.BeginInvoke(new Action(() => IsBusy = isBusy));
        }
        else
        {
            IsBusy = isBusy;
        }
    }

    private void UpdateIsLoadingModDetails(bool isLoading)
    {
        if (Application.Current?.Dispatcher is Dispatcher dispatcher)
        {
            if (dispatcher.CheckAccess())
                IsLoadingModDetails = isLoading;
            else
                dispatcher.BeginInvoke(new Action(() => IsLoadingModDetails = isLoading));
        }
        else
        {
            IsLoadingModDetails = isLoading;
        }
    }

    private void UpdateModDetailsProgressVisibility()
    {
        IsModDetailsProgressVisible = _isLoadingModDetails && !_isFastCheckInProgress;
    }


    private void UpdateLoadedModsStatus()
    {
        if (_modDetailsProgressTracker.IsRefreshPending)
        {
            if (!_isModDetailsStatusActive) SetStatus(BuildModDetailsLoadingStatusMessage(), false, true);
        }
        else
        {
            SetStatus(BuildModDetailsReadyStatusMessage(), false);
        }
    }

    private void OnModDetailsRefreshEnqueued(int count, string? statusText = null)
        => _modDetailsProgressTracker.OnRefreshEnqueued(count, statusText);

    private void OnModDetailsRefreshCompleted(int completedCount = 1)
        => _modDetailsProgressTracker.OnRefreshCompleted(completedCount);

    private string BuildModDetailsLoadingStatusMessage()
    {
        return $"Loaded {TotalMods} mods. Loading mod details...";
    }

    private string BuildModDetailsReadyStatusMessage()
    {
        if (_hasShownModDetailsLoadingStatus) return $"Loaded {TotalMods} mods. Mod details up to date.";

        return $"Loaded {TotalMods} mods.";
    }

    private static Task InvokeOnDispatcherAsync(Action action, CancellationToken cancellationToken,
        DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (cancellationToken.IsCancellationRequested) return Task.CompletedTask;

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, priority, cancellationToken).Task;
        }

        action();
        return Task.CompletedTask;
    }

    private static Task<T> InvokeOnDispatcherAsync<T>(Func<T> function, CancellationToken cancellationToken,
        DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<T>(cancellationToken);

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess()) return Task.FromResult(function());

            return dispatcher.InvokeAsync(function, priority, cancellationToken).Task;
        }

        return Task.FromResult(function());
    }

    private bool FilterMod(object? item)
    {
        if (item is not ModListItemViewModel mod) return false;

        if (!_tagFilterService.PassesInstalledTagFilter(mod.DatabaseTags))
            return false;

        if (_searchTokens.Length == 0) return true;

        return mod.MatchesSearchTokens(_searchTokens);
    }

    private static string[] CreateSearchTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

        return value
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private string GetDisplayPath(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;

        string best;
        try
        {
            best = Path.GetFullPath(fullPath);
        }
        catch (Exception)
        {
            return fullPath;
        }

        // Use cached base paths for performance - recalculating these for every mod is expensive
        var basePaths = _cachedBasePaths ??= GetBasePathsList();

        foreach (var candidate in basePaths)
            try
            {
                var relative = Path.GetRelativePath(candidate, best);
                if (!relative.StartsWith("..", StringComparison.Ordinal) && relative.Length < best.Length)
                    best = relative;
            }
            catch (Exception)
            {
                // Ignore invalid paths.
            }

        return best.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private List<string> GetBasePathsList()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;

            try
            {
                var full = Path.GetFullPath(candidate);
                set.Add(full);
            }
            catch (Exception)
            {
                // Ignore invalid paths.
            }
        }

        TryAdd(_settingsStore.DataDirectory);
        foreach (var path in _settingsStore.SearchBaseCandidates) TryAdd(path);

        TryAdd(Directory.GetCurrentDirectory());

        return set.ToList();
    }

    private static IEnumerable<SortOption> CreateSortOptions()
    {
        yield return new SortOption(
            "Name (A → Z)",
            (nameof(ModListItemViewModel.NameSortKey), ListSortDirection.Ascending));
        yield return new SortOption(
            "Name (Z → A)",
            (nameof(ModListItemViewModel.NameSortKey), ListSortDirection.Descending));
        yield return new SortOption(
            "Active (Active → Inactive)",
            (nameof(ModListItemViewModel.ActiveSortOrder), ListSortDirection.Ascending),
            (nameof(ModListItemViewModel.NameSortKey), ListSortDirection.Ascending));
        yield return new SortOption(
            "Active (Inactive → Active)",
            (nameof(ModListItemViewModel.ActiveSortOrder), ListSortDirection.Descending),
            (nameof(ModListItemViewModel.NameSortKey), ListSortDirection.Ascending));
    }

    private void ReapplyActiveSortIfNeeded()
    {
        if (SelectedSortOption?.SortDescriptions is not { Count: > 0 } sorts) return;

        var primary = sorts[0];
        if (!IsActiveSortProperty(primary.Property)) return;

        SelectedSortOption.Apply(ModsView);
        ModsView.Refresh();
    }

    private static bool IsActiveSortProperty(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return false;

        return string.Equals(propertyName, nameof(ModListItemViewModel.IsActive), StringComparison.OrdinalIgnoreCase)
               || string.Equals(propertyName, nameof(ModListItemViewModel.ActiveSortOrder),
                   StringComparison.OrdinalIgnoreCase);
    }

    private void SetStatus(string message, bool isError, bool isModDetailsStatus = false)
    {
        StatusLogService.AppendStatus(message, isError);
        StatusMessage = message;
        IsErrorStatus = isError;
        _isModDetailsStatusActive = isModDetailsStatus;
        _hasShownModDetailsLoadingStatus = isModDetailsStatus;
    }

    private Dictionary<string, ModEntry?> LoadChangedModEntries(
        IReadOnlyCollection<string> paths,
        IReadOnlyDictionary<string, ModEntry>? existingEntries)
    {
        var results = new Dictionary<string, ModEntry?>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var entry = _discoveryService.LoadModFromPath(path);
            if (entry != null)
            {
                ResetCalculatedModState(entry);
                if (existingEntries != null && existingEntries.TryGetValue(path, out var previous))
                    CopyTransientModState(previous, entry);
            }

            results[path] = entry;
        }

        return results;
    }

    private static void ResetCalculatedModState(ModEntry entry)
    {
        entry.LoadError = null;
        entry.DependencyHasErrors = false;
        entry.MissingDependencies = Array.Empty<ModDependencyInfo>();
    }

    private static void CopyTransientModState(ModEntry source, ModEntry target)
    {
        if (source is null || target is null) return;

        var sameModId = string.Equals(source.ModId, target.ModId, StringComparison.OrdinalIgnoreCase);
        var sameVersion = string.Equals(source.Version, target.Version, StringComparison.OrdinalIgnoreCase)
                          || (string.IsNullOrWhiteSpace(source.Version) && string.IsNullOrWhiteSpace(target.Version));

        if (!sameModId || !sameVersion) return;

        if (target.DatabaseInfo is null && source.DatabaseInfo != null) target.DatabaseInfo = source.DatabaseInfo;

        if (source.ModDatabaseSearchScore.HasValue) target.ModDatabaseSearchScore = source.ModDatabaseSearchScore;
    }

    private void ApplyPartialUpdates(
        IReadOnlyDictionary<string, ModEntry?> changes,
        string? previousSelection,
        IReadOnlyCollection<ModEntry>? statusChanges = null)
    {
        var hasStatusChanges = statusChanges is { Count: > 0 };

        if (changes.Count == 0 && !hasStatusChanges)
        {
            SetStatus("Mods are up to date.", false);
            if (!string.IsNullOrWhiteSpace(previousSelection)
                && _modViewModelsBySourcePath.TryGetValue(previousSelection, out var selected))
                SelectedMod = selected;

            return;
        }

        var added = 0;
        var updated = 0;
        var removed = 0;

        // Use batch operation to minimize UI notifications during updates
        using (_mods.SuspendNotifications())
        {
            foreach (var change in changes)
            {
                var path = change.Key;
                var entry = change.Value;

                if (entry == null)
                {
                    if (_modViewModelsBySourcePath.TryGetValue(path, out var existingVm))
                    {
                        _mods.Remove(existingVm);
                        _modViewModelsBySourcePath.Remove(path);
                        removed++;

                        if (ReferenceEquals(SelectedMod, existingVm)) SelectedMod = null;
                    }

                    _modEntriesBySourcePath.Remove(path);
                    continue;
                }

                var viewModel = CreateModViewModel(entry);
                if (_modViewModelsBySourcePath.TryGetValue(path, out var existing))
                {
                    var index = _mods.IndexOf(existing);
                    if (index >= 0)
                        _mods[index] = viewModel;
                    else
                        _mods.Add(viewModel);

                    _modViewModelsBySourcePath[path] = viewModel;
                    _modEntriesBySourcePath[path] = entry;  // Update entry dictionary for updated mods
                    updated++;

                    if (ReferenceEquals(SelectedMod, existing)) SelectedMod = viewModel;
                }
                else
                {
                    _mods.Add(viewModel);
                    _modViewModelsBySourcePath[path] = viewModel;
                    _modEntriesBySourcePath[path] = entry;
                    added++;
                }
            }
        }

        if (statusChanges is { Count: > 0 })
        {
            foreach (var entry in statusChanges)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.SourcePath)) continue;
                if (changes.ContainsKey(entry.SourcePath)) continue;

                if (_modViewModelsBySourcePath.TryGetValue(entry.SourcePath, out var viewModel))
                {
                    viewModel.UpdateLoadError(entry.LoadError);
                    viewModel.UpdateDependencyIssues(
                        entry.DependencyHasErrors,
                        entry.MissingDependencies ?? Array.Empty<ModDependencyInfo>());
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(previousSelection)
            && _modViewModelsBySourcePath.TryGetValue(previousSelection, out var selectedAfter))
            SelectedMod = selectedAfter;
        else if (SelectedMod != null && !_mods.Contains(SelectedMod)) SelectedMod = null;

        var affected = added + updated + removed;
        if (affected == 0)
        {
            SetStatus(hasStatusChanges ? "Updated mod statuses." : "Mods are up to date.", false);
            return;
        }

        var parts = new List<string>();
        if (added > 0) parts.Add($"{added} added");

        if (updated > 0) parts.Add($"{updated} updated");

        if (removed > 0) parts.Add($"{removed} removed");

        var summary = parts.Count == 0 ? $"{affected} changed" : string.Join(", ", parts);
        SetStatus($"Applied changes to mods ({summary}).", false);
    }

    private void QueueDatabaseInfoRefresh(IEnumerable<ModEntry> entries, bool forceRefresh = false)
        => _databaseInfoRefreshService.QueueRefresh(entries, forceRefresh);

    private async Task ApplyDatabaseInfoBatchAsync(IReadOnlyList<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)> batch)
    {
        if (batch.Count == 0) return;

        try
        {
            var dispatcherStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await InvokeOnDispatcherAsync(
                    () =>
                    {
                        // Record dispatcher wait time once for the entire batch
                        dispatcherStopwatch.Stop();
                        _timingService.RecordDbApplyDispatcherTime(dispatcherStopwatch.Elapsed.TotalMilliseconds);

                        // Apply all updates in the batch
                        using (_timingService.MeasureDbApplyUiHandler(batch.Count))
                        {
                            foreach (var (entry, info, loadLogoImmediately) in batch)
                            {
                                if (!_modEntriesBySourcePath.TryGetValue(entry.SourcePath, out var currentEntry)
                                    || !ReferenceEquals(currentEntry, entry))
                                    continue;

                                // Measure entry update time
                                using (_timingService.MeasureDbApplyEntryUpdate())
                                {
                                    currentEntry.UpdateDatabaseInfo(info);
                                }

                                if (_modViewModelsBySourcePath.TryGetValue(entry.SourcePath, out var viewModel))
                                {
                                    // Measure view model update time
                                    using (_timingService.MeasureDbApplyViewModelUpdate())
                                    {
                                        viewModel.UpdateDatabaseInfo(info, loadLogoImmediately);
                                    }
                                }
                            }
                        }
                    },
                    CancellationToken.None,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellations when the dispatcher shuts down.
        }
        catch (Exception ex)
        {
            // Log batch application failures to diagnose missing UI updates
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to apply database info batch ({batch.Count} items): {ex.Message}");
        }
    }

    private async Task ApplyDatabaseInfoImmediateAsync(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)
    {
        try
        {
            var dispatcherStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await InvokeOnDispatcherAsync(
                    () =>
                    {
                        // Record dispatcher wait time
                        dispatcherStopwatch.Stop();
                        _timingService.RecordDbApplyDispatcherTime(dispatcherStopwatch.Elapsed.TotalMilliseconds);

                        if (!_modEntriesBySourcePath.TryGetValue(entry.SourcePath, out var currentEntry)
                            || !ReferenceEquals(currentEntry, entry))
                            return;

                        using (_timingService.MeasureDbApplyUiHandler(1))
                        {
                            // Measure entry update time
                            using (_timingService.MeasureDbApplyEntryUpdate())
                            {
                                currentEntry.UpdateDatabaseInfo(info);
                            }

                            if (_modViewModelsBySourcePath.TryGetValue(entry.SourcePath, out var viewModel))
                            {
                                // Measure view model update time
                                using (_timingService.MeasureDbApplyViewModelUpdate())
                                {
                                    viewModel.UpdateDatabaseInfo(info, loadLogoImmediately);
                                    // Defer user report refresh to avoid cascading updates during bulk loading
                                    // The user report will be loaded on-demand when visible or when explicitly requested
                                }
                            }
                        }
                    },
                    CancellationToken.None,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellations when the dispatcher shuts down.
        }
        catch (Exception)
        {
            // Ignore dispatcher failures to keep refresh resilient.
        }
    }

    private void OnInternetAccessChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                RefreshInternetAccessDependentState();
            else
                dispatcher.BeginInvoke(DispatcherPriority.Normal, RefreshInternetAccessDependentState);

            return;
        }

        RefreshInternetAccessDependentState();
    }

    private void RefreshInternetAccessDependentState()
    {
        var isOffline = InternetAccessManager.IsInternetAccessDisabled;

        foreach (var mod in _mods)
        {
            mod.RefreshInternetAccessDependentState();
            if (isOffline)
                mod.SetUserReportOffline();
            else
                QueueUserReportRefresh(mod);
        }

        foreach (var mod in _searchResults) mod.RefreshInternetAccessDependentState();

        _tabNavigation.NotifyInternetAccessChanged();
        OnPropertyChanged(nameof(CanAccessCloudModlists));

        if (InternetAccessManager.IsInternetAccessDisabled && _tabNavigation.Current == ViewSection.DatabaseTab)
        {
            SetStatus(TabNavigationViewModel.InternetAccessDisabledStatusMessage, false);
            _tabNavigation.SetViewSection(ViewSection.MainTab);
            return;
        }

        if (InternetAccessManager.IsInternetAccessDisabled && _tabNavigation.Current == ViewSection.ModlistTab)
        {
            SetStatus(TabNavigationViewModel.InternetAccessDisabledStatusMessage, false);
            _tabNavigation.SetViewSection(ViewSection.MainTab);
        }
    }

    public sealed class ModUserReportChangedEventArgs : EventArgs
    {
        public ModUserReportChangedEventArgs(
            string modId,
            string? modVersion,
            int? numericModId,
            ModVersionVoteSummary summary)
        {
            ModId = modId;
            ModVersion = modVersion;
            NumericModId = numericModId;
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        public string ModId { get; }

        public string? ModVersion { get; }

        public int? NumericModId { get; }

        public ModVersionVoteSummary Summary { get; }
    }

    private void PerformClientSettingsCleanupIfNeeded()
    {
        if (_configuration.ClientSettingsCleanupCompleted) return;

        try
        {
            _settingsStore.RemoveInvalidDisabledEntries();
            _configuration.SetClientSettingsCleanupCompleted();
        }
        catch (Exception)
        {
            // This cleanup is a best-effort operation.
        }
    }

    /// <summary>
    /// Calculates an adaptive debounce delay based on the collection size.
    /// Larger collections use longer delays to reduce CPU load during rapid typing.
    /// </summary>
    private TimeSpan CalculateAdaptiveSearchDebounce()
    {
        var modCount = _mods.Count;

        if (modCount <= LargeModListThreshold)
        {
            return InstalledModsSearchDebounceMin;
        }

        // Scale linearly between min and max based on collection size
        // At LargeModListThreshold (200) mods: min delay (100ms)
        // At VeryLargeModListThreshold (500)+ mods: max delay (300ms)
        var debounceRange = VeryLargeModListThreshold - LargeModListThreshold;
        var scale = Math.Min(1.0, (modCount - LargeModListThreshold) / (double)debounceRange);
        var delayMs = InstalledModsSearchDebounceMin.TotalMilliseconds +
                      (InstalledModsSearchDebounceMax.TotalMilliseconds - InstalledModsSearchDebounceMin.TotalMilliseconds) * scale;

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private void TriggerDebouncedInstalledModsSearch()
    {
        lock (_searchDebounceLock)
        {
            if (_disposed) return;

            // Cancel any pending search
            _pendingSearchCts?.Cancel();
            _pendingSearchCts?.Dispose();
            _pendingSearchCts = new CancellationTokenSource();

            // Calculate adaptive debounce based on collection size
            var debounceDelay = CalculateAdaptiveSearchDebounce();

            // Initialize or restart the debounce timer
            // Note: Pass null to callback as we check _pendingSearchCts inside the callback
            // instead of capturing it in a closure, which prevents stale CTS references
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new Timer(OnSearchDebounceTimerElapsed, null,
                    debounceDelay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _searchDebounceTimer.Change(debounceDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnSearchDebounceTimerElapsed(object? state)
    {
        // Get the current CTS inside the callback to avoid capturing stale references
        CancellationTokenSource? cts;
        lock (_searchDebounceLock)
        {
            if (_disposed) return;
            cts = _pendingSearchCts;
        }

        if (cts == null) return;
        ExecuteInstalledModsSearch(cts);
    }

    private void ExecuteInstalledModsSearch(CancellationTokenSource cts)
    {
        if (cts.IsCancellationRequested) return;

        // Check disposal and verify this is still the current pending search
        bool shouldExecute;
        lock (_searchDebounceLock)
        {
            if (_disposed) return;
            shouldExecute = ReferenceEquals(cts, _pendingSearchCts);
        }

        if (!shouldExecute) return;

        // Execute the search on the UI thread using Input priority for better responsiveness
        // Input priority ensures search results appear quickly while still yielding to rendering
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(new Action(() => RefreshModsViewIfNotCancelled(cts)),
                DispatcherPriority.Input);
        }
        else
        {
            RefreshModsViewIfNotCancelled(cts);
        }
    }

    private void RefreshModsViewIfNotCancelled(CancellationTokenSource cts)
    {
        if (!cts.IsCancellationRequested)
        {
            ModsView.Refresh();
        }
    }
}
