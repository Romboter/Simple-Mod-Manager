#nullable enable
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void EditThemeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ShowCustomThemeEditor();
    }

    private void RefreshCustomThemeMenuItems()
    {
        if (ThemesMenuItem is null) return;

        foreach (var item in _customThemeMenuItems) ThemesMenuItem.Items.Remove(item);

        _customThemeMenuItems.Clear();

        ThemeMenu.RefreshFromConfiguration();

        if (ThemeMenu.CustomThemes.Count == 0)
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
        foreach (var themeItem in ThemeMenu.CustomThemes)
        {
            var menuItem = new MenuItem
            {
                Header = themeItem.Name,
                Height = 35,
                IsCheckable = true,
                Style = toggleStyle,
                Command = ThemeMenu.SelectCustomThemeCommand,
                CommandParameter = themeItem.Name
            };
            menuItem.SetBinding(MenuItem.IsCheckedProperty,
                new System.Windows.Data.Binding(nameof(CustomThemeMenuItemViewModel.IsSelected)) { Source = themeItem, Mode = System.Windows.Data.BindingMode.OneWay });

            _customThemeMenuItems.Add(menuItem);
            ThemesMenuItem.Items.Insert(separatorIndex, menuItem);
            separatorIndex++; // Increment to keep adding before the separator
        }
    }

    private void ShowCustomThemeEditor()
    {
        ThemeMenu.RefreshFromConfiguration();

        var palette = _userConfiguration.GetThemePaletteColors();
        App.ApplyTheme(_userConfiguration.ColorTheme, palette.Count > 0 ? palette : null);
        ClearScrollViewerCache();

        var dialog = new ThemePaletteEditorDialog(_userConfiguration)
        {
            Owner = this
        };

        _ = dialog.ShowDialog();

        RefreshCustomThemeMenuItems();
        ClearScrollViewerCache();
    }
}
