#nullable enable
using CommunityToolkit.Mvvm.Input;
using ModernWpf.Controls;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimpleVsManager.Cloud;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using UglyToad.PdfPig;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using YamlDotNet.Core;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ModUserReportChangedEventArgs = VintageStoryModManager.ViewModels.MainViewModel.ModUserReportChangedEventArgs;
using Colors = QuestPDF.Helpers.Colors;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursors = System.Windows.Input.Cursors;
using DataFolderBackupProgress = VintageStoryModManager.Services.DataBackupProgress;
using DataFolderBackupSummary = VintageStoryModManager.Services.DataBackupSummary;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using FileRecycleOption = Microsoft.VisualBasic.FileIO.RecycleOption;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;
using FileUIOption = Microsoft.VisualBasic.FileIO.UIOption;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Point = System.Windows.Point;
using ProgressBar = System.Windows.Controls.ProgressBar;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using VerticalAlignment = System.Windows.VerticalAlignment;
using WinForms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;
using WpfToolTip = System.Windows.Controls.ToolTip;

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

    public MainWindow()
        {
            RefreshModsUiCommand = new AsyncRelayCommand(
                RefreshModsWithErrorHandlingAsync,
                AsyncRelayCommandOptions.AllowConcurrentExecutions);

            _userConfiguration = new UserConfigurationService();
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

            InitializeComponent();

            InitializeModBrowserView();

            DeveloperProfileManager.CurrentProfileChanged += DeveloperProfileManager_OnCurrentProfileChanged;

            RootGrid.SizeChanged += RootGrid_OnSizeChanged;

            UpdateModlistLoadingUiState();

            InitializeColumnVisibilityMenu();

            ApplyStoredWindowDimensions();
            CacheAllVersionsMenuItem.IsChecked = _userConfiguration.CacheAllVersionsLocally;
            RequireExactVsVersionMenuItem.IsChecked = _userConfiguration.RequireExactVsVersionMatch;
            DisableAutoRefreshMenuItem.IsChecked = _userConfiguration.DisableAutoRefresh;
            DisableInternetAccessMenuItem.IsChecked = _userConfiguration.DisableInternetAccess;
            AutomaticDataBackupsMenuItem.IsChecked = _userConfiguration.AutomaticDataBackupsEnabled;
            InternetAccessManager.SetInternetAccessDisabled(_userConfiguration.DisableInternetAccess);
            UpdateServerOptionsState(_userConfiguration.EnableServerOptions);
            UseFasterThumbnailsMenuItem.IsChecked = _userConfiguration.UseFasterThumbnails;
            DisableHoverEffectsMenuItem.IsChecked = _userConfiguration.DisableHoverEffects;
            HoverEffectHelper.SetDisableHoverEffects(this, _userConfiguration.DisableHoverEffects);
            UpdateLoggingMenuState();
            InitializeTraceListener();
            _modActivityLoggingService.LogAppLaunch();

            RefreshCustomThemeMenuItems();
            UpdateThemeMenuSelection(_userConfiguration.ColorTheme, _userConfiguration.GetCurrentThemeName());

            if (ManagerVersionMenuItem is not null)
            {
                var managerVersion = GetManagerInformationalVersion();
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

            UpdateModlistAutoLoadMenu(_userConfiguration.ModlistAutoLoadBehavior);

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

    private delegate bool PathValidator(string? path, out string? normalizedPath, out string? errorMessage);
}
