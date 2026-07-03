#nullable enable

using System.Windows;
using System.Windows.Controls;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void CacheAllVersionsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetCacheAllVersionsLocally(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.CacheAllVersionsLocally;
    }

    private void RequireExactVsVersionMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetRequireExactVsVersionMatch(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.RequireExactVsVersionMatch;
    }
}
