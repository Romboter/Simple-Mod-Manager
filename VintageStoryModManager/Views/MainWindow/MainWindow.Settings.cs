#nullable enable
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void DisableAutoRefreshMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        var disable = menuItem.IsChecked;

        if (disable && !_userConfiguration.DisableAutoRefreshWarningAcknowledged)
        {
            var message =
                "This will disable automatic refresh functions such as update checks, loading of tags and other mod details, user reports and other similar functions." +
                Environment.NewLine + Environment.NewLine +
                "This will decrease loading times on start for example. Use the \"Refresh\" button to choose when you want to fetch details from cache and/or Mod DB. This dialog will not be shown again.";

            var confirmation = WpfMessageBox.Show(
                message,
                "Simple VS Manager",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                menuItem.IsChecked = false;
                return;
            }

            _userConfiguration.SetDisableAutoRefreshWarningAcknowledged(true);
        }

        _userConfiguration.SetDisableAutoRefresh(disable);
        _viewModel?.SetAutoRefreshDisabled(disable);
    }

    private void AutomaticDataBackupsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        if (menuItem.IsChecked && (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory)))
        {
            WpfMessageBox.Show(
                "Please configure a valid VintagestoryData folder before enabling automatic backups.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            menuItem.IsChecked = false;
            return;
        }

        if (menuItem.IsChecked && !_userConfiguration.AutomaticDataBackupsWarningAcknowledged)
        {
            const string message =
                "This is an experimental feature and will increase your start up time depending on your files. Depending on your mods, saves and other files it could also end up taking up a lot of disk space."
                + "\n\n"
                + "But probably not. Report any bugs! Remember to launch with the \"Launch Vintage Story\" button, it backups at launch ONLY.";

            WpfMessageBox.Show(
                message,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _userConfiguration.SetAutomaticDataBackupsWarningAcknowledged(true);
        }

        _userConfiguration.SetAutomaticDataBackupsEnabled(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.AutomaticDataBackupsEnabled;
    }

    private void DisableInternetAccessMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        var isDisabled = menuItem.IsChecked;
        InternetAccessManager.SetInternetAccessDisabled(isDisabled);
        _userConfiguration.SetDisableInternetAccess(isDisabled);

        _viewModel?.OnInternetAccessStateChanged();
    }

    private void AlwaysClearModlistsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        HandleModlistAutoLoadMenuClick(
            sender,
            ModlistAutoLoadBehavior.Replace,
            ModlistAutoLoadBehavior.Prompt);
    }

    private void AlwaysAddModlistsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        HandleModlistAutoLoadMenuClick(
            sender,
            ModlistAutoLoadBehavior.Add,
            ModlistAutoLoadBehavior.Prompt);
    }

    private void HandleModlistAutoLoadMenuClick(object sender, ModlistAutoLoadBehavior enabledBehavior,
            ModlistAutoLoadBehavior disabledBehavior)
    {
        if (sender is not MenuItem menuItem) return;

        var newBehavior = menuItem.IsChecked ? enabledBehavior : disabledBehavior;
        SetModlistAutoLoadBehavior(newBehavior);
    }

    private void SetModlistAutoLoadBehavior(ModlistAutoLoadBehavior behavior)
    {
        UpdateModlistAutoLoadMenu(behavior);
        _userConfiguration.SetModlistAutoLoadBehavior(behavior);
    }

    private void UpdateModlistAutoLoadMenu(ModlistAutoLoadBehavior behavior)
    {
        if (AlwaysClearModlistsMenuItem is not null)
            AlwaysClearModlistsMenuItem.IsChecked = behavior == ModlistAutoLoadBehavior.Replace;

        if (AlwaysAddModlistsMenuItem is not null)
            AlwaysAddModlistsMenuItem.IsChecked = behavior == ModlistAutoLoadBehavior.Add;
    }

    private void UpdateGameVersionMenuItem(string? gameVersion)
    {
        if (GameVersionMenuItem is null) return;

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            GameVersionMenuItem.Visibility = Visibility.Collapsed;
            return;
        }

        GameVersionMenuItem.Header = $"Vintage Story: {gameVersion}";
        GameVersionMenuItem.Visibility = Visibility.Visible;
    }

    private void UseFasterThumbnailsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetUseFasterThumbnails(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.UseFasterThumbnails;
    }
}
