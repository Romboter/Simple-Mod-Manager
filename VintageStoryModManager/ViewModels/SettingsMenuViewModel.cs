using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Bound view-model for the settings/logging toggle menu items. Owns the toggle state and the
///     acknowledge-once warning gates; dialogs go through IConfirmationService, side effects that
///     belong to the window (trace listener, view-model notifications) through the callbacks.
/// </summary>
public sealed class SettingsMenuViewModel : ObservableObject
{
    private readonly IUserConfigurationService _configuration;
    private readonly IConfirmationService _confirmation;
    private readonly Func<string?> _dataDirectoryProvider;
    private readonly Action<bool> _onAutoRefreshDisabledChanged;
    private readonly Action _onInternetAccessStateChanged;
    private readonly Action _onErrorLoggingChanged;

    private bool _disableAutoRefresh;
    private bool _automaticDataBackupsEnabled;
    private bool _disableInternetAccess;
    private bool _useFasterThumbnails;
    private bool _cacheAllVersionsLocally;
    private bool _requireExactVsVersionMatch;
    private bool _logModUpdates;
    private bool _logModInstalls;
    private bool _logModDeletions;
    private bool _logAppLaunchAndExit;
    private bool _logErrorsAndExceptions;
    private bool _alwaysClearModlists;
    private bool _alwaysAddModlists;

    public SettingsMenuViewModel(
        IUserConfigurationService configuration,
        IConfirmationService confirmation,
        Func<string?> dataDirectoryProvider,
        Action<bool> onAutoRefreshDisabledChanged,
        Action onInternetAccessStateChanged,
        Action onErrorLoggingChanged)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _dataDirectoryProvider = dataDirectoryProvider ?? throw new ArgumentNullException(nameof(dataDirectoryProvider));
        _onAutoRefreshDisabledChanged = onAutoRefreshDisabledChanged ?? throw new ArgumentNullException(nameof(onAutoRefreshDisabledChanged));
        _onInternetAccessStateChanged = onInternetAccessStateChanged ?? throw new ArgumentNullException(nameof(onInternetAccessStateChanged));
        _onErrorLoggingChanged = onErrorLoggingChanged ?? throw new ArgumentNullException(nameof(onErrorLoggingChanged));

        _disableAutoRefresh = _configuration.DisableAutoRefresh;
        _automaticDataBackupsEnabled = _configuration.AutomaticDataBackupsEnabled;
        _disableInternetAccess = _configuration.DisableInternetAccess;
        _useFasterThumbnails = _configuration.UseFasterThumbnails;
        _cacheAllVersionsLocally = _configuration.CacheAllVersionsLocally;
        _requireExactVsVersionMatch = _configuration.RequireExactVsVersionMatch;
        _logModUpdates = _configuration.LogModUpdates;
        _logModInstalls = _configuration.LogModInstalls;
        _logModDeletions = _configuration.LogModDeletions;
        _logAppLaunchAndExit = _configuration.LogAppLaunchAndExit;
        _logErrorsAndExceptions = _configuration.LogErrorsAndExceptions;
        _alwaysClearModlists = _configuration.ModlistAutoLoadBehavior == ModlistAutoLoadBehavior.Replace;
        _alwaysAddModlists = _configuration.ModlistAutoLoadBehavior == ModlistAutoLoadBehavior.Add;

        ToggleDisableAutoRefreshCommand = new AsyncRelayCommand(ToggleDisableAutoRefreshAsync);
        ToggleAutomaticDataBackupsCommand = new AsyncRelayCommand(ToggleAutomaticDataBackupsAsync);
    }

    // --- gated toggles: OneWay-bound property + command ---
    public bool DisableAutoRefresh
    {
        get => _disableAutoRefresh;
        private set => SetProperty(ref _disableAutoRefresh, value);
    }

    public IAsyncRelayCommand ToggleDisableAutoRefreshCommand { get; }

    public bool AutomaticDataBackupsEnabled
    {
        get => _automaticDataBackupsEnabled;
        private set => SetProperty(ref _automaticDataBackupsEnabled, value);
    }

    public IAsyncRelayCommand ToggleAutomaticDataBackupsCommand { get; }

    // --- plain two-way toggles ---
    public bool DisableInternetAccess
    {
        get => _disableInternetAccess;
        set
        {
            if (!SetProperty(ref _disableInternetAccess, value)) return;

            InternetAccessManager.SetInternetAccessDisabled(value);
            _configuration.SetDisableInternetAccess(value);
            _onInternetAccessStateChanged();
        }
    }

    public bool UseFasterThumbnails
    {
        get => _useFasterThumbnails;
        set
        {
            if (!SetProperty(ref _useFasterThumbnails, value)) return;

            _configuration.SetUseFasterThumbnails(value);
            SetProperty(ref _useFasterThumbnails, _configuration.UseFasterThumbnails, nameof(UseFasterThumbnails));
        }
    }

    public bool CacheAllVersionsLocally
    {
        get => _cacheAllVersionsLocally;
        set
        {
            if (!SetProperty(ref _cacheAllVersionsLocally, value)) return;

            _configuration.SetCacheAllVersionsLocally(value);
            SetProperty(ref _cacheAllVersionsLocally, _configuration.CacheAllVersionsLocally, nameof(CacheAllVersionsLocally));
        }
    }

    public bool RequireExactVsVersionMatch
    {
        get => _requireExactVsVersionMatch;
        set
        {
            if (!SetProperty(ref _requireExactVsVersionMatch, value)) return;

            _configuration.SetRequireExactVsVersionMatch(value);
            SetProperty(ref _requireExactVsVersionMatch, _configuration.RequireExactVsVersionMatch, nameof(RequireExactVsVersionMatch));
        }
    }

    public bool LogModUpdates
    {
        get => _logModUpdates;
        set
        {
            if (!SetProperty(ref _logModUpdates, value)) return;

            _configuration.SetLogModUpdates(value);
            SetProperty(ref _logModUpdates, _configuration.LogModUpdates, nameof(LogModUpdates));
        }
    }

    public bool LogModInstalls
    {
        get => _logModInstalls;
        set
        {
            if (!SetProperty(ref _logModInstalls, value)) return;

            _configuration.SetLogModInstalls(value);
            SetProperty(ref _logModInstalls, _configuration.LogModInstalls, nameof(LogModInstalls));
        }
    }

    public bool LogModDeletions
    {
        get => _logModDeletions;
        set
        {
            if (!SetProperty(ref _logModDeletions, value)) return;

            _configuration.SetLogModDeletions(value);
            SetProperty(ref _logModDeletions, _configuration.LogModDeletions, nameof(LogModDeletions));
        }
    }

    public bool LogAppLaunchAndExit
    {
        get => _logAppLaunchAndExit;
        set
        {
            if (!SetProperty(ref _logAppLaunchAndExit, value)) return;

            _configuration.SetLogAppLaunchAndExit(value);
            SetProperty(ref _logAppLaunchAndExit, _configuration.LogAppLaunchAndExit, nameof(LogAppLaunchAndExit));
        }
    }

    public bool LogErrorsAndExceptions
    {
        get => _logErrorsAndExceptions;
        set
        {
            if (!SetProperty(ref _logErrorsAndExceptions, value)) return;

            _configuration.SetLogErrorsAndExceptions(value);
            SetProperty(ref _logErrorsAndExceptions, _configuration.LogErrorsAndExceptions, nameof(LogErrorsAndExceptions));
            _onErrorLoggingChanged();
        }
    }

    // --- modlist auto-load pair (mutually exclusive; unchecking either -> Prompt) ---
    public bool AlwaysClearModlists
    {
        get => _alwaysClearModlists;
        set
        {
            if (!SetProperty(ref _alwaysClearModlists, value)) return;

            var behavior = value ? ModlistAutoLoadBehavior.Replace : ModlistAutoLoadBehavior.Prompt;
            _configuration.SetModlistAutoLoadBehavior(behavior);

            if (value && _alwaysAddModlists)
                SetProperty(ref _alwaysAddModlists, false, nameof(AlwaysAddModlists));
        }
    }

    public bool AlwaysAddModlists
    {
        get => _alwaysAddModlists;
        set
        {
            if (!SetProperty(ref _alwaysAddModlists, value)) return;

            var behavior = value ? ModlistAutoLoadBehavior.Add : ModlistAutoLoadBehavior.Prompt;
            _configuration.SetModlistAutoLoadBehavior(behavior);

            if (value && _alwaysClearModlists)
                SetProperty(ref _alwaysClearModlists, false, nameof(AlwaysClearModlists));
        }
    }

    private async Task ToggleDisableAutoRefreshAsync()
    {
        var disable = !DisableAutoRefresh;

        if (disable && !_configuration.DisableAutoRefreshWarningAcknowledged)
        {
            var message =
                "This will disable automatic refresh functions such as update checks, loading of tags and other mod details, user reports and other similar functions." +
                Environment.NewLine + Environment.NewLine +
                "This will decrease loading times on start for example. Use the \"Refresh\" button to choose when you want to fetch details from cache and/or Mod DB. This dialog will not be shown again.";

            if (!await _confirmation.ConfirmAsync(message, "Simple VS Manager"))
                return; // declined: property untouched, menu stays unchecked via OneWay binding

            _configuration.SetDisableAutoRefreshWarningAcknowledged(true);
        }

        _configuration.SetDisableAutoRefresh(disable);
        DisableAutoRefresh = disable;
        _onAutoRefreshDisabledChanged(disable);
    }

    private async Task ToggleAutomaticDataBackupsAsync()
    {
        var enable = !AutomaticDataBackupsEnabled;

        if (enable)
        {
            var dataDirectory = _dataDirectoryProvider();
            if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory))
            {
                await _confirmation.NotifyAsync(
                    "Please configure a valid VintagestoryData folder before enabling automatic backups.",
                    "Simple VS Manager");
                return;
            }

            if (!_configuration.AutomaticDataBackupsWarningAcknowledged)
            {
                const string message =
                    "This is an experimental feature and will increase your start up time depending on your files. Depending on your mods, saves and other files it could also end up taking up a lot of disk space."
                    + "\n\n"
                    + "But probably not. Report any bugs! Remember to launch with the \"Launch Vintage Story\" button, it backups at launch ONLY.";

                await _confirmation.NotifyAsync(message, "Simple VS Manager");

                _configuration.SetAutomaticDataBackupsWarningAcknowledged(true);
            }
        }

        _configuration.SetAutomaticDataBackupsEnabled(enable);
        AutomaticDataBackupsEnabled = _configuration.AutomaticDataBackupsEnabled;
    }
}
