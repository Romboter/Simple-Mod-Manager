#nullable enable
using CommunityToolkit.Mvvm.Input;
using SimpleVsManager.Cloud;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.ViewModels;
using Point = System.Windows.Point;

namespace VintageStoryModManager.Views;

public partial class MainWindow : Window
{

    private const int MaxDataBackupsMenuItems = 15;

    private const int WindowPositionScreenMargin = 50;

    private const string DiscordInviteUrl = "https://discord.gg/Zhm3QnD2s9";

    private static readonly string ManagerModDatabaseUrl = DevConfig.ManagerModDatabaseUrl;

    private static readonly string ManagerModDatabaseModId = DevConfig.ManagerModDatabaseModId;

    private static readonly string ModDatabaseUnavailableMessage = DevConfig.ModDatabaseUnavailableMessage;

    private static readonly double ModListScrollMultiplier = DevConfig.ModListScrollMultiplier;

    private static readonly double ModDbDesignScrollMultiplier = DevConfig.ModDbDesignScrollMultiplier;

    private static readonly double LoadMoreScrollThreshold = DevConfig.LoadMoreScrollThreshold;

    private static readonly double HoverOverlayOpacity = DevConfig.HoverOverlayOpacity;

    private static readonly double SelectionOverlayOpacity = DevConfig.SelectionOverlayOpacity;

    private static readonly double ModInfoPanelHorizontalOverhang = DevConfig.ModInfoPanelHorizontalOverhang;

    private static readonly double DefaultModInfoPanelLeft = DevConfig.DefaultModInfoPanelLeft;

    private static readonly double DefaultModInfoPanelTop = DevConfig.DefaultModInfoPanelTop;

    private static readonly double DefaultModInfoPanelRightMargin = DevConfig.DefaultModInfoPanelRightMargin;

    private static readonly string PresetDirectoryName = DevConfig.PresetDirectoryName;

    private static readonly string ModListDirectoryName = DevConfig.ModListDirectoryName;

    private static readonly string CloudModListCacheDirectoryName = DevConfig.CloudModListCacheDirectoryName;

    private static readonly string RebuiltModListDirectoryName = DevConfig.RebuiltModListDirectoryName;

    private static readonly int AutomaticConfigMaxWordDistance = DevConfig.AutomaticConfigMaxWordDistance;

    private static readonly HttpClient ConnectivityTestHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

    private static readonly PresetLoadOptions StandardPresetLoadOptions = new(true, false, false);

    private static readonly PresetLoadOptions ModListLoadOptions = new(true, true, true);

    private static readonly DependencyProperty BoundModProperty =
            DependencyProperty.RegisterAttached(
                "BoundMod",
                typeof(ModListItemViewModel),
                typeof(MainWindow));

    private static readonly DependencyProperty BoundModHandlerProperty =
            DependencyProperty.RegisterAttached(
                "BoundModHandler",
                typeof(PropertyChangedEventHandler),
                typeof(MainWindow));

    private static readonly DependencyProperty RowIsHoveredProperty =
            DependencyProperty.RegisterAttached(
                "RowIsHovered",
                typeof(bool),
                typeof(MainWindow));

    public static readonly DependencyProperty IsDataBackupInProgressProperty =
            DependencyProperty.Register(
                nameof(IsDataBackupInProgress),
                typeof(bool),
                typeof(MainWindow),
                new PropertyMetadata(false));

    public static readonly DependencyProperty DataBackupProgressProperty =
            DependencyProperty.Register(
                nameof(DataBackupProgress),
                typeof(double),
                typeof(MainWindow),
                new PropertyMetadata(0d));

    public static readonly DependencyProperty DataBackupStatusMessageProperty =
            DependencyProperty.Register(
                nameof(DataBackupStatusMessage),
                typeof(string),
                typeof(MainWindow),
                new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsModlistInstallInProgressProperty =
            DependencyProperty.Register(
                nameof(IsModlistInstallInProgress),
                typeof(bool),
                typeof(MainWindow),
                new PropertyMetadata(false));

    public static readonly DependencyProperty ModlistInstallProgressProperty =
            DependencyProperty.Register(
                nameof(ModlistInstallProgress),
                typeof(double),
                typeof(MainWindow),
                new PropertyMetadata(0d));

    public static readonly DependencyProperty ModlistInstallStatusMessageProperty =
            DependencyProperty.Register(
                nameof(ModlistInstallStatusMessage),
                typeof(string),
                typeof(MainWindow),
                new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ModlistDownloadSpeedProperty =
            DependencyProperty.Register(
                nameof(ModlistDownloadSpeed),
                typeof(string),
                typeof(MainWindow),
                new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HasModlistDownloadSpeedProperty =
            DependencyProperty.Register(
                nameof(HasModlistDownloadSpeed),
                typeof(bool),
                typeof(MainWindow),
                new PropertyMetadata(false));

    public bool IsDataBackupInProgress
        {
            get => (bool)GetValue(IsDataBackupInProgressProperty);
            set => SetValue(IsDataBackupInProgressProperty, value);
        }

    public double DataBackupProgress
        {
            get => (double)GetValue(DataBackupProgressProperty);
            set => SetValue(DataBackupProgressProperty, value);
        }

    public string DataBackupStatusMessage
        {
            get => (string)GetValue(DataBackupStatusMessageProperty);
            set => SetValue(DataBackupStatusMessageProperty, value);
        }

    public bool IsModlistInstallInProgress
        {
            get => (bool)GetValue(IsModlistInstallInProgressProperty);
            set => SetValue(IsModlistInstallInProgressProperty, value);
        }

    public double ModlistInstallProgress
        {
            get => (double)GetValue(ModlistInstallProgressProperty);
            set => SetValue(ModlistInstallProgressProperty, value);
        }

    public string ModlistInstallStatusMessage
        {
            get => (string)GetValue(ModlistInstallStatusMessageProperty);
            set => SetValue(ModlistInstallStatusMessageProperty, value);
        }

    public string ModlistDownloadSpeed
        {
            get => (string)GetValue(ModlistDownloadSpeedProperty);
            set => SetValue(ModlistDownloadSpeedProperty, value);
        }

    public bool HasModlistDownloadSpeed
        {
            get => (bool)GetValue(HasModlistDownloadSpeedProperty);
            set => SetValue(HasModlistDownloadSpeedProperty, value);
        }

    private readonly SemaphoreSlim _backupSemaphore = new(1, 1);

    private readonly SemaphoreSlim _cloudStoreLock = new(1, 1);

    private readonly List<MenuItem> _developerProfileMenuItems = new();

    private readonly List<MenuItem> _gameProfileMenuItems = new();

    private readonly Dictionary<InstalledModsColumn, bool> _installedColumnVisibilityPreferences = new();

    private readonly ModCompatibilityCommentsService _modCompatibilityCommentsService = new();

    private readonly ModDatabaseService _modDatabaseService = new();

    private readonly ModUpdateService _modUpdateService = new();

    private readonly DataFolderBackupCoordinator _dataFolderBackupCoordinator;

    private readonly ModActivityLoggingService _modActivityLoggingService;

    private readonly UserConfigurationService _userConfiguration;

    private readonly ServerTargetService _serverTargetService;

    private readonly SyncEngine _syncEngine = new();

    private ModManagerTraceListener? _traceListener;

    private readonly ModGridSelectionService _modSelection;

    private readonly List<LocalModlistListEntry> _selectedLocalModlists = new();

    private CloudModlistListEntry? _selectedCloudModlist;

    private bool _cloudModlistsLoaded;

    private bool _firebaseMigrationAttempted;

    private FirebaseModlistStore? _cloudModlistStore;

    private ICollectionView? _currentModsView;

    private ScrollViewer? _modsScrollViewer;

    private DataGrid? _modsScrollViewerSource;

    private INotifyCollectionChanged? _modsCollection;

    private string? _customShortcutPath;

    private string? _dataDirectory;

    private string? _gameDirectory;

    private GameSessionMonitor? _gameSessionMonitor;

    private DispatcherTimer? _modsWatcherTimer;

    private ModUsagePromptData? _modUsagePromptData;

    private VotesCacheWatcher? _votesCacheWatcher;

    private bool _hasAppliedInitialModInfoPanelPosition;

    private bool _isApplyingMultiToggle;

    private bool _isApplyingPreset;

    private bool _isAutomaticRefreshRunning;

    private bool _isCloudModlistRefreshInProgress;

    private bool _isDependencyResolutionRefreshPending;

    private bool _isDraggingModInfoPanel;

    private bool _isInitializing;

    private bool _isUpdatingModlistsTabSelection;

    private bool _isUpdatingMiddleTabSelection;

    private bool _isModUpdateInProgress;

    private bool _isModUsageDialogOpen;

    private bool _isRefreshingAfterModlistLoad;

    private bool _isWindowActive;

    private bool _refreshAfterModlistLoadPending;

    private bool _localModlistsLoaded;

    private bool _suppressSortPreferenceSave;

    private bool _isModBrowserWatcherSubscribed;

    private Point _modInfoDragOffset;

    private string? _recentLocalModBackupDirectory;

    private List<string>? _recentLocalModBackupModNames;

    private int _modlistInstallCompletedSteps;

    private int _modlistInstallTotalSteps;

    private MainViewModel? _viewModel;

    private ModBrowserViewModel? _modBrowserViewModel;

    private readonly List<MenuItem> _customThemeMenuItems = new();

    private readonly IConfirmationService _confirmationService;

    public MainWindow()
        {
            RefreshModsUiCommand = new AsyncRelayCommand(
                RefreshModsWithErrorHandlingAsync,
                AsyncRelayCommandOptions.AllowConcurrentExecutions);

            _userConfiguration = new UserConfigurationService();
            _confirmationService = new ConfirmationService();
            _dataFolderBackupCoordinator = new DataFolderBackupCoordinator(new DataBackupService(
                _userConfiguration.GetConfigurationDirectory(),
                _userConfiguration.CustomDataBackupLocation));
            _modActivityLoggingService = new ModActivityLoggingService(_userConfiguration);
            _serverTargetService = new ServerTargetService(_userConfiguration.GetConfigurationDirectory());
            _modSelection = new ModGridSelectionService(
                Dispatcher,
                UpdateSelectedModButtons,
                UpdateSelectedModFixButton,
                UpdateSelectedModCopyForServerButton);

            SettingsMenu = new SettingsMenuViewModel(
                _userConfiguration,
                _confirmationService,
                () => _dataDirectory,
                disable => _viewModel?.SetAutoRefreshDisabled(disable),
                () => _viewModel?.OnInternetAccessStateChanged(),
                InitializeTraceListener);

            ThemeMenu = new ThemeMenuViewModel(
                _userConfiguration,
                (theme, palette) => App.ApplyTheme(theme, palette),
                ClearScrollViewerCache);

            InitializeComponent();

            InitializeModBrowserView();

            DeveloperProfileManager.CurrentProfileChanged += DeveloperProfileManager_OnCurrentProfileChanged;

            RootGrid.SizeChanged += RootGrid_OnSizeChanged;

            UpdateModlistLoadingUiState();

            InitializeColumnVisibilityMenu();

            ApplyStoredWindowDimensions();
            InternetAccessManager.SetInternetAccessDisabled(_userConfiguration.DisableInternetAccess);
            UpdateServerOptionsState(_userConfiguration.EnableServerOptions);
            DisableHoverEffectsMenuItem.IsChecked = _userConfiguration.DisableHoverEffects;
            HoverEffectHelper.SetDisableHoverEffects(this, _userConfiguration.DisableHoverEffects);
            InitializeTraceListener();
            _modActivityLoggingService.LogAppLaunch();

            RefreshCustomThemeMenuItems();

            if (ManagerVersionMenuItem is not null)
            {
                var managerVersion = ManagerVersionHelper.GetManagerInformationalVersion();
                if (string.IsNullOrWhiteSpace(managerVersion))
                {
                    ManagerVersionMenuItem.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ManagerVersionMenuItem.Header = $"Version: {managerVersion}";
                    ManagerVersionMenuItem.Visibility = Visibility.Visible;
                }
            }

            TryInitializePaths();
            RefreshDeveloperProfilesMenuEntries();
            UpdateGameProfileMenuChecks();
            UpdateActiveGameProfileDisplay();
            UpdateSyncToServerMenuState();

            UpdateGameVersionMenuItem(VintageStoryVersionLocator.GetInstalledVersion(_gameDirectory));

            if (!string.IsNullOrWhiteSpace(_dataDirectory))
                try
                {
                    InitializeViewModel();
                }
                catch (Exception ex)
                {
                    HandleViewModelInitializationFailure(ex);
                }

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_OnClosing;
            InternetAccessManager.InternetAccessChanged += InternetAccessManager_OnInternetAccessChanged;

            UpdateCloudModlistControlsEnabledState();
            UpdateLocalModlistControlsEnabledState();
        }

    public IAsyncRelayCommand RefreshModsUiCommand { get; }

    public SettingsMenuViewModel SettingsMenu { get; }

    public ThemeMenuViewModel ThemeMenu { get; }

    private delegate bool PathValidator(string? path, out string? normalizedPath, out string? errorMessage);
}
