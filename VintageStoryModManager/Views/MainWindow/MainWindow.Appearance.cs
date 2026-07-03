#nullable enable
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void ThemeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        if (menuItem.Tag is string themeName)
        {
            if (!_userConfiguration.TryActivateTheme(themeName)) return;

            var selectedPalette = _userConfiguration.GetThemePaletteColors();
            UpdateThemeMenuSelection(_userConfiguration.ColorTheme, themeName);
            App.ApplyTheme(_userConfiguration.ColorTheme, selectedPalette.Count > 0 ? selectedPalette : null);
            return;
        }

        if (menuItem.Tag is not ColorTheme theme) return;

        var currentTheme = _userConfiguration.ColorTheme;
        IReadOnlyDictionary<string, string>? paletteOverride = null;

        if (theme == ColorTheme.SurpriseMe) paletteOverride = SurprisePaletteGenerator.GenerateSurprisePalette();

        if (theme == currentTheme && paletteOverride is null)
        {
            UpdateThemeMenuSelection(currentTheme, _userConfiguration.GetCurrentThemeName());
            return;
        }

        UpdateThemeMenuSelection(theme, UserConfigurationService.GetThemeDisplayName(theme));
        _userConfiguration.SetColorTheme(theme, paletteOverride);
        var palette = _userConfiguration.GetThemePaletteColors();
        App.ApplyTheme(theme, palette.Count > 0 ? palette : null);
        ClearScrollViewerCache();
    }

    private void EditThemeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ShowCustomThemeEditor();
    }

    private void UpdateThemeMenuSelection(ColorTheme theme, string? themeName = null)
    {
        if (VintageStoryThemeMenuItem is not null)
            VintageStoryThemeMenuItem.IsChecked = theme == ColorTheme.VintageStory;

        if (DarkThemeMenuItem is not null) DarkThemeMenuItem.IsChecked = theme == ColorTheme.Dark;

        if (LightThemeMenuItem is not null) LightThemeMenuItem.IsChecked = theme == ColorTheme.Light;

        var normalizedName = themeName ?? _userConfiguration.GetCurrentThemeName();
        foreach (var menuItem in _customThemeMenuItems)
        {
            var header = menuItem.Header?.ToString();
            menuItem.IsChecked = theme == ColorTheme.Custom
                                 && !string.IsNullOrWhiteSpace(header)
                                 && string.Equals(header, normalizedName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void RefreshCustomThemeMenuItems()
    {
        if (ThemesMenuItem is null) return;

        foreach (var item in _customThemeMenuItems)
        {
            item.Click -= ThemeMenuItem_OnClick;
            ThemesMenuItem.Items.Remove(item);
        }

        _customThemeMenuItems.Clear();

        var customThemes = _userConfiguration.GetCustomThemeNames();

        if (customThemes.Count == 0)
        {
            if (CustomThemesSeparator is not null)
                CustomThemesSeparator.Visibility = Visibility.Collapsed;
            return;
        }

        if (CustomThemesSeparator is not null)
            CustomThemesSeparator.Visibility = Visibility.Visible;

        var toggleStyle = TryFindResource("ToggleMenuItemStyle") as Style;

        // Find the separator index
        var separatorIndex = CustomThemesSeparator is not null && ThemesMenuItem.Items.Contains(CustomThemesSeparator)
            ? ThemesMenuItem.Items.IndexOf(CustomThemesSeparator)
            : ThemesMenuItem.Items.Count;

        // Insert custom themes BEFORE the separator
        foreach (var name in customThemes)
        {
            var menuItem = new MenuItem
            {
                Header = name,
                Height = 35,
                IsCheckable = true,
                Tag = name,
                Style = toggleStyle
            };

            menuItem.Click += ThemeMenuItem_OnClick;
            _customThemeMenuItems.Add(menuItem);
            ThemesMenuItem.Items.Insert(separatorIndex, menuItem);
            separatorIndex++; // Increment to keep adding before the separator
        }
    }

    private void ShowCustomThemeEditor()
    {
        var currentTheme = _userConfiguration.ColorTheme;
        var currentThemeName = _userConfiguration.GetCurrentThemeName();

        UpdateThemeMenuSelection(currentTheme, currentThemeName);

        var palette = _userConfiguration.GetThemePaletteColors();
        App.ApplyTheme(currentTheme, palette.Count > 0 ? palette : null);
        ClearScrollViewerCache();

        var dialog = new ThemePaletteEditorDialog(_userConfiguration)
        {
            Owner = this
        };

        _ = dialog.ShowDialog();

        RefreshCustomThemeMenuItems();
        UpdateThemeMenuSelection(_userConfiguration.ColorTheme, _userConfiguration.GetCurrentThemeName());
        ClearScrollViewerCache();

    }
}
