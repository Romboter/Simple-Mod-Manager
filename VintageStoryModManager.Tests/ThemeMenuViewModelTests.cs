using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ThemeMenuViewModelTests
{
    private sealed class FakeUserConfiguration : IUserConfigurationService
    {
        public bool DisableAutoRefresh { get; set; }
        public bool DisableAutoRefreshWarningAcknowledged { get; set; }
        public void SetDisableAutoRefreshWarningAcknowledged(bool value) => DisableAutoRefreshWarningAcknowledged = value;
        public void SetDisableAutoRefresh(bool value) => DisableAutoRefresh = value;

        public bool AutomaticDataBackupsEnabled { get; set; }
        public bool AutomaticDataBackupsWarningAcknowledged { get; set; }
        public void SetAutomaticDataBackupsWarningAcknowledged(bool value) => AutomaticDataBackupsWarningAcknowledged = value;
        public void SetAutomaticDataBackupsEnabled(bool value) => AutomaticDataBackupsEnabled = value;

        public bool DisableInternetAccess { get; set; }
        public void SetDisableInternetAccess(bool value) => DisableInternetAccess = value;

        public ModlistAutoLoadBehavior ModlistAutoLoadBehavior { get; set; } = ModlistAutoLoadBehavior.Prompt;
        public void SetModlistAutoLoadBehavior(ModlistAutoLoadBehavior value) => ModlistAutoLoadBehavior = value;

        public bool UseFasterThumbnails { get; set; }
        public void SetUseFasterThumbnails(bool value) => UseFasterThumbnails = value;

        public bool CacheAllVersionsLocally { get; set; }
        public void SetCacheAllVersionsLocally(bool value) => CacheAllVersionsLocally = value;

        public bool RequireExactVsVersionMatch { get; set; }
        public void SetRequireExactVsVersionMatch(bool value) => RequireExactVsVersionMatch = value;

        public bool LogModUpdates { get; set; }
        public void SetLogModUpdates(bool value) => LogModUpdates = value;

        public bool LogModInstalls { get; set; }
        public void SetLogModInstalls(bool value) => LogModInstalls = value;

        public bool LogModDeletions { get; set; }
        public void SetLogModDeletions(bool value) => LogModDeletions = value;

        public bool LogAppLaunchAndExit { get; set; }
        public void SetLogAppLaunchAndExit(bool value) => LogAppLaunchAndExit = value;

        public bool LogErrorsAndExceptions { get; set; }
        public void SetLogErrorsAndExceptions(bool value) => LogErrorsAndExceptions = value;

        public ColorTheme ColorTheme { get; set; } = ColorTheme.VintageStory;
        public string CurrentThemeName { get; set; } = "Vintage Story";
        public List<string> CustomThemeNames { get; } = new();
        public Dictionary<string, string> ThemePaletteColors { get; } = new();
        public List<(ColorTheme Theme, IReadOnlyDictionary<string, string>? Palette)> SetColorThemeCalls { get; } = new();
        public string? LastActivatedThemeName { get; private set; }
        public bool TryActivateThemeResult { get; set; } = true;

        public bool TryActivateTheme(string? name)
        {
            LastActivatedThemeName = name;
            if (!TryActivateThemeResult) return false;
            ColorTheme = ColorTheme.Custom;
            if (name is not null) CurrentThemeName = name;
            return true;
        }

        public IReadOnlyDictionary<string, string> GetThemePaletteColors() => ThemePaletteColors;
        public string GetCurrentThemeName() => CurrentThemeName;
        public IReadOnlyList<string> GetCustomThemeNames() => CustomThemeNames;

        public void SetColorTheme(ColorTheme theme, IReadOnlyDictionary<string, string>? paletteOverride = null)
        {
            SetColorThemeCalls.Add((theme, paletteOverride));
            ColorTheme = theme;
            if (theme != ColorTheme.Custom) CurrentThemeName = theme.ToString();
        }
    }

    private sealed record Fixture(
        ThemeMenuViewModel ViewModel,
        FakeUserConfiguration Configuration,
        List<(ColorTheme Theme, IReadOnlyDictionary<string, string>? Palette)> ApplyCalls,
        List<int> ClearCacheCalls);

    private static Fixture CreateFixture()
    {
        var configuration = new FakeUserConfiguration();
        var applyCalls = new List<(ColorTheme Theme, IReadOnlyDictionary<string, string>? Palette)>();
        var clearCacheCalls = new List<int>();

        var viewModel = new ThemeMenuViewModel(
            configuration,
            (theme, palette) => applyCalls.Add((theme, palette)),
            () => clearCacheCalls.Add(clearCacheCalls.Count));

        return new Fixture(viewModel, configuration, applyCalls, clearCacheCalls);
    }

    [Fact]
    public void Ctor_SeedsSelectionAndCustomThemes()
    {
        var configuration = new FakeUserConfiguration { ColorTheme = ColorTheme.Dark };
        configuration.CustomThemeNames.Add("Ocean");
        configuration.CustomThemeNames.Add("Sunset");

        var applyCalls = new List<(ColorTheme Theme, IReadOnlyDictionary<string, string>? Palette)>();
        var viewModel = new ThemeMenuViewModel(configuration, (theme, palette) => applyCalls.Add((theme, palette)), () => { });

        Assert.True(viewModel.IsDarkSelected);
        Assert.False(viewModel.IsVintageStorySelected);
        Assert.False(viewModel.IsLightSelected);
        Assert.Equal(2, viewModel.CustomThemes.Count);
        Assert.Empty(applyCalls);
    }

    [Fact]
    public void SelectBuiltIn_NewTheme_SetsConfig_Applies_ClearsCache()
    {
        var fixture = CreateFixture();

        fixture.ViewModel.SelectBuiltInThemeCommand.Execute(ColorTheme.Dark);

        var call = Assert.Single(fixture.Configuration.SetColorThemeCalls);
        Assert.Equal(ColorTheme.Dark, call.Theme);
        Assert.Null(call.Palette);
        var applyCall = Assert.Single(fixture.ApplyCalls);
        Assert.Equal(ColorTheme.Dark, applyCall.Theme);
        Assert.Single(fixture.ClearCacheCalls);
        Assert.True(fixture.ViewModel.IsDarkSelected);
    }

    [Fact]
    public void SelectBuiltIn_SameTheme_NoConfigWrite_NoApply_ButRaisesPropertyChanged()
    {
        var fixture = CreateFixture(); // starts at VintageStory

        var propertyChangedCount = 0;
        fixture.ViewModel.PropertyChanged += (_, _) => propertyChangedCount++;

        fixture.ViewModel.SelectBuiltInThemeCommand.Execute(ColorTheme.VintageStory);

        Assert.Empty(fixture.Configuration.SetColorThemeCalls);
        Assert.Empty(fixture.ApplyCalls);
        Assert.Empty(fixture.ClearCacheCalls);
        Assert.True(propertyChangedCount > 0);
    }

    [Fact]
    public void SelectBuiltIn_SurpriseMe_PassesGeneratedPalette()
    {
        var fixture = CreateFixture();
        fixture.Configuration.ColorTheme = ColorTheme.SurpriseMe;

        fixture.ViewModel.SelectBuiltInThemeCommand.Execute(ColorTheme.SurpriseMe);

        var call = Assert.Single(fixture.Configuration.SetColorThemeCalls);
        Assert.Equal(ColorTheme.SurpriseMe, call.Theme);
        Assert.NotNull(call.Palette);
    }

    [Fact]
    public void SelectCustom_ActivatesByName_Applies_NoCacheClear()
    {
        var fixture = CreateFixture();
        fixture.Configuration.CustomThemeNames.Add("Ocean");
        fixture.ViewModel.RefreshFromConfiguration();

        fixture.ViewModel.SelectCustomThemeCommand.Execute("Ocean");

        Assert.Equal("Ocean", fixture.Configuration.LastActivatedThemeName);
        Assert.Single(fixture.ApplyCalls);
        Assert.Empty(fixture.ClearCacheCalls);
        var item = Assert.Single(fixture.ViewModel.CustomThemes);
        Assert.True(item.IsSelected);
    }

    [Fact]
    public void SelectCustom_ActivationFails_NoApply()
    {
        var fixture = CreateFixture();
        fixture.Configuration.CustomThemeNames.Add("Ocean");
        fixture.ViewModel.RefreshFromConfiguration();
        fixture.Configuration.TryActivateThemeResult = false;

        fixture.ViewModel.SelectCustomThemeCommand.Execute("Ocean");

        Assert.Empty(fixture.ApplyCalls);
        var item = Assert.Single(fixture.ViewModel.CustomThemes);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void SelectCustom_NullParameter_NoOp()
    {
        var fixture = CreateFixture();

        var exception = Record.Exception(() => fixture.ViewModel.SelectCustomThemeCommand.Execute(null));

        Assert.Null(exception);
        Assert.Empty(fixture.ApplyCalls);
        Assert.Null(fixture.Configuration.LastActivatedThemeName);
    }

    [Fact]
    public void RefreshFromConfiguration_RebuildsList_AndSelection()
    {
        var fixture = CreateFixture();
        fixture.Configuration.CustomThemeNames.Add("Ocean");
        fixture.Configuration.ColorTheme = ColorTheme.Custom;
        fixture.Configuration.CurrentThemeName = "OCEAN";

        fixture.ViewModel.RefreshFromConfiguration();

        var item = Assert.Single(fixture.ViewModel.CustomThemes);
        Assert.Equal("Ocean", item.Name);
        Assert.True(item.IsSelected);
    }
}
