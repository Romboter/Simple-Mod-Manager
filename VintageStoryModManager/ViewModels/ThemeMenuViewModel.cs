using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Bound view-model for the Themes menu. Owns theme selection state and the switch/activate
///     logic; applying the theme to the running app and clearing view caches are window concerns
///     injected as callbacks. The window still constructs the custom-theme MenuItems (dynamic menu
///     population is view code) but binds each one to an entry in <see cref="CustomThemes" />.
/// </summary>
public sealed class ThemeMenuViewModel : ObservableObject
{
    private readonly IUserConfigurationService _configuration;
    private readonly Action<ColorTheme, IReadOnlyDictionary<string, string>?> _applyTheme;   // (t, p) => App.ApplyTheme(t, p)
    private readonly Action _clearScrollViewerCache;                                          // () => ClearScrollViewerCache()

    private bool _isVintageStorySelected;
    private bool _isDarkSelected;
    private bool _isLightSelected;

    public ThemeMenuViewModel(
        IUserConfigurationService configuration,
        Action<ColorTheme, IReadOnlyDictionary<string, string>?> applyTheme,
        Action clearScrollViewerCache)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _applyTheme = applyTheme ?? throw new ArgumentNullException(nameof(applyTheme));
        _clearScrollViewerCache = clearScrollViewerCache ?? throw new ArgumentNullException(nameof(clearScrollViewerCache));

        SelectBuiltInThemeCommand = new RelayCommand<ColorTheme>(SelectBuiltInTheme);
        SelectCustomThemeCommand = new RelayCommand<string>(SelectCustomTheme);

        RefreshFromConfiguration();
    }

    public bool IsVintageStorySelected => _isVintageStorySelected;
    public bool IsDarkSelected => _isDarkSelected;
    public bool IsLightSelected => _isLightSelected;

    public ObservableCollection<CustomThemeMenuItemViewModel> CustomThemes { get; } = new();

    public IRelayCommand<ColorTheme> SelectBuiltInThemeCommand { get; }
    public IRelayCommand<string> SelectCustomThemeCommand { get; }

    /// <summary>Re-reads the custom-theme list and current selection from configuration.
    /// Called by the window after the palette-editor dialog closes (and once from this ctor).</summary>
    public void RefreshFromConfiguration()
    {
        var names = _configuration.GetCustomThemeNames();

        CustomThemes.Clear();
        foreach (var name in names) CustomThemes.Add(new CustomThemeMenuItemViewModel(name));

        RefreshSelection();
    }

    // Body: the `menuItem.Tag is string themeName` branch of ThemeMenuItem_OnClick, verbatim,
    // with UpdateThemeMenuSelection(...) replaced by RefreshSelection() and App.ApplyTheme by _applyTheme.
    private void SelectCustomTheme(string? themeName)
    {
        if (themeName is null) return;   // RelayCommand<string> parameter guard (adaptation: replaces the Tag type-test)

        if (!_configuration.TryActivateTheme(themeName)) return;

        var selectedPalette = _configuration.GetThemePaletteColors();
        RefreshSelection();
        _applyTheme(_configuration.ColorTheme, selectedPalette.Count > 0 ? selectedPalette : null);
    }

    // Body: the `menuItem.Tag is not ColorTheme theme` branch, verbatim, same substitutions.
    private void SelectBuiltInTheme(ColorTheme theme)
    {
        var currentTheme = _configuration.ColorTheme;
        IReadOnlyDictionary<string, string>? paletteOverride = null;

        if (theme == ColorTheme.SurpriseMe) paletteOverride = SurprisePaletteGenerator.GenerateSurprisePalette();

        if (theme == currentTheme && paletteOverride is null)
        {
            RefreshSelection();
            return;
        }

        _configuration.SetColorTheme(theme, paletteOverride);
        var palette = _configuration.GetThemePaletteColors();
        RefreshSelection();
        _applyTheme(theme, palette.Count > 0 ? palette : null);
        _clearScrollViewerCache();
    }

    // Replaces UpdateThemeMenuSelection. IMPORTANT: raises PropertyChanged UNCONDITIONALLY.
    // A checkable MenuItem click writes IsChecked via SetCurrentValue; when the user clicks the
    // already-active theme the VM value doesn't change, so only an unconditional raise makes the
    // OneWay binding re-assert and keep the checkmark visible (mirrors the original handler's
    // "same theme → UpdateThemeMenuSelection and return" re-sync).
    private void RefreshSelection()
    {
        var theme = _configuration.ColorTheme;

        _isVintageStorySelected = theme == ColorTheme.VintageStory;
        _isDarkSelected = theme == ColorTheme.Dark;
        _isLightSelected = theme == ColorTheme.Light;
        OnPropertyChanged(nameof(IsVintageStorySelected));
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(IsLightSelected));

        var normalizedName = _configuration.GetCurrentThemeName();
        foreach (var item in CustomThemes)
            item.SetSelected(theme == ColorTheme.Custom
                             && !string.IsNullOrWhiteSpace(item.Name)
                             && string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>One entry in the custom-themes submenu.</summary>
public sealed class CustomThemeMenuItemViewModel : ObservableObject
{
    public CustomThemeMenuItemViewModel(string name) => Name = name;

    public string Name { get; }

    public bool IsSelected { get; private set; }

    // Unconditional raise, same rationale as ThemeMenuViewModel.RefreshSelection.
    internal void SetSelected(bool value)
    {
        IsSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }
}
