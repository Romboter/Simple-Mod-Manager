#nullable enable

using System.Windows;
using System.Windows.Controls;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void InitializeColumnVisibilityMenu()
    {
        RegisterColumnMenuItem(ActiveColumnMenuItem, InstalledModsColumn.Active);
        RegisterColumnMenuItem(IconColumnMenuItem, InstalledModsColumn.Icon);
        RegisterColumnMenuItem(NameColumnMenuItem, InstalledModsColumn.Name);
        RegisterColumnMenuItem(VersionColumnMenuItem, InstalledModsColumn.Version);
        RegisterColumnMenuItem(LatestVersionColumnMenuItem, InstalledModsColumn.LatestVersion);
        RegisterColumnMenuItem(AuthorsColumnMenuItem, InstalledModsColumn.Authors);
        RegisterColumnMenuItem(TagsColumnMenuItem, InstalledModsColumn.Tags);
        RegisterColumnMenuItem(UserReportsColumnMenuItem, InstalledModsColumn.UserReports);
        RegisterColumnMenuItem(StatusColumnMenuItem, InstalledModsColumn.Status);
        RegisterColumnMenuItem(SideColumnMenuItem, InstalledModsColumn.Side);
    }

    private void RegisterColumnMenuItem(MenuItem? menuItem, InstalledModsColumn column)
    {
        if (menuItem == null) return;

        menuItem.Tag = column;
        if (_userConfiguration.GetInstalledColumnVisibility(column.ToString()) is bool storedVisibility)
            menuItem.IsChecked = storedVisibility;
        _installedColumnVisibilityPreferences[column] = menuItem.IsChecked;
        NotifyViewModelOfInstalledColumnVisibility(column, menuItem.IsChecked);
        ApplyInstalledColumnVisibility(column, menuItem.IsChecked);
        menuItem.Checked += InstalledModsColumnMenuItem_OnChecked;
        menuItem.Unchecked += InstalledModsColumnMenuItem_OnChecked;
    }

    private void InstalledModsColumnMenuItem_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not InstalledModsColumn column) return;

        _installedColumnVisibilityPreferences[column] = menuItem.IsChecked;
        _userConfiguration.SetInstalledColumnVisibility(column.ToString(), menuItem.IsChecked);
        NotifyViewModelOfInstalledColumnVisibility(column, menuItem.IsChecked);
        ApplyInstalledColumnVisibility(column, menuItem.IsChecked);
    }

    private void ApplyInstalledColumnVisibility(InstalledModsColumn columnKey, bool isVisible)
    {
        DataGridColumn? column = columnKey switch
        {
            InstalledModsColumn.Active => ActiveColumn,
            InstalledModsColumn.Icon => IconColumn,
            InstalledModsColumn.Name => NameColumn,
            InstalledModsColumn.Version => VersionColumn,
            InstalledModsColumn.LatestVersion => LatestVersionColumn,
            InstalledModsColumn.Authors => AuthorsColumn,
            InstalledModsColumn.Tags => TagsColumn,
            InstalledModsColumn.UserReports => UserReportsColumn,
            InstalledModsColumn.Status => StatusColumn,
            InstalledModsColumn.Side => SideColumn,
            _ => null
        };

        if (column == null) return;

        column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NotifyViewModelOfInstalledColumnVisibility(InstalledModsColumn column, bool isVisible)
    {
        _viewModel?.SetInstalledColumnVisibility(column.ToString(), isVisible);
    }

    private void ApplyColumnVisibilityPreferencesToViewModel()
    {
        if (_viewModel is null) return;

        foreach (var pair in _installedColumnVisibilityPreferences)
            NotifyViewModelOfInstalledColumnVisibility(pair.Key, pair.Value);
    }
}
