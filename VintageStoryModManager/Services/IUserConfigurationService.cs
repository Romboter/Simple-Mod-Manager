namespace VintageStoryModManager.Services
{
    /// <summary>
    ///     Narrow seam over <see cref="UserConfigurationService"/> exposing exactly the getter/setter
    ///     pairs used by <see cref="VintageStoryModManager.ViewModels.SettingsMenuViewModel"/>, so tests
    ///     can fake it without touching the real user-config file.
    /// </summary>
    public interface IUserConfigurationService
    {
        bool DisableAutoRefresh { get; }
        bool DisableAutoRefreshWarningAcknowledged { get; }
        void SetDisableAutoRefreshWarningAcknowledged(bool value);
        void SetDisableAutoRefresh(bool value);

        bool AutomaticDataBackupsEnabled { get; }
        bool AutomaticDataBackupsWarningAcknowledged { get; }
        void SetAutomaticDataBackupsWarningAcknowledged(bool value);
        void SetAutomaticDataBackupsEnabled(bool value);

        bool DisableInternetAccess { get; }
        void SetDisableInternetAccess(bool value);

        ModlistAutoLoadBehavior ModlistAutoLoadBehavior { get; }
        void SetModlistAutoLoadBehavior(ModlistAutoLoadBehavior value);

        bool UseFasterThumbnails { get; }
        void SetUseFasterThumbnails(bool value);

        bool CacheAllVersionsLocally { get; }
        void SetCacheAllVersionsLocally(bool value);

        bool RequireExactVsVersionMatch { get; }
        void SetRequireExactVsVersionMatch(bool value);

        bool LogModUpdates { get; }
        void SetLogModUpdates(bool value);

        bool LogModInstalls { get; }
        void SetLogModInstalls(bool value);

        bool LogModDeletions { get; }
        void SetLogModDeletions(bool value);

        bool LogAppLaunchAndExit { get; }
        void SetLogAppLaunchAndExit(bool value);

        bool LogErrorsAndExceptions { get; }
        void SetLogErrorsAndExceptions(bool value);

        ColorTheme ColorTheme { get; }
        bool TryActivateTheme(string? name);
        IReadOnlyDictionary<string, string> GetThemePaletteColors();
        string GetCurrentThemeName();
        IReadOnlyList<string> GetCustomThemeNames();
        void SetColorTheme(ColorTheme theme, IReadOnlyDictionary<string, string>? paletteOverride = null);
    }
}
