#nullable enable

using System;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void RefreshDeveloperProfilesMenuEntries()
    {
        if (DeveloperProfilesMenuItem is null) return;

        foreach (var item in _developerProfileMenuItems) item.Click -= DeveloperProfileMenuItem_OnClick;

        _developerProfileMenuItems.Clear();
        DeveloperProfilesMenuItem.Items.Clear();

        if (!DeveloperProfileManager.DevDebug)
        {
            DeveloperProfilesMenuItem.Visibility = Visibility.Collapsed;
            return;
        }

        var profiles = DeveloperProfileManager.GetProfiles();
        if (profiles.Count == 0)
        {
            DeveloperProfilesMenuItem.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var profile in profiles)
        {
            var menuItem = new MenuItem
            {
                Header = profile.DisplayName,
                Tag = profile,
                IsCheckable = true
            };

            menuItem.Click += DeveloperProfileMenuItem_OnClick;
            DeveloperProfilesMenuItem.Items.Add(menuItem);
            _developerProfileMenuItems.Add(menuItem);
        }

        DeveloperProfilesMenuItem.Visibility = Visibility.Visible;
        UpdateDeveloperProfileMenuChecks();
    }

    private void UpdateDeveloperProfileMenuChecks()
    {
        if (!DeveloperProfileManager.DevDebug) return;

        var current = DeveloperProfileManager.CurrentProfile;

        foreach (var menuItem in _developerProfileMenuItems)
        {
            if (menuItem.Tag is not DeveloperProfile profile)
            {
                menuItem.IsChecked = false;
                continue;
            }

            var isSelected = current is not null
                             && string.Equals(profile.Id, current.Id, StringComparison.OrdinalIgnoreCase);
            menuItem.IsChecked = isSelected;
        }
    }

    private async void DeveloperProfileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: DeveloperProfile profile }) return;

        var changed = DeveloperProfileManager.TrySetCurrentProfile(profile.Id);
        if (!changed)
        {
            UpdateDeveloperProfileMenuChecks();
            return;
        }

        var profileDirectory = profile.DataDirectory;

        if (string.Equals(_dataDirectory, profileDirectory, StringComparison.OrdinalIgnoreCase))
        {
            UpdateDeveloperProfileMenuChecks();
            return;
        }

        _dataDirectory = profileDirectory;

        if (profile.IsOriginal)
        {
            _userConfiguration.SetDataDirectory(profileDirectory);
            DeveloperProfileManager.UpdateOriginalProfile(profileDirectory);
        }

        _cloudModlistStore = null;
        await ReloadViewModelAsync();
        UpdateDeveloperProfileMenuChecks();
    }

    private void DeveloperProfileManager_OnCurrentProfileChanged(object? sender, DeveloperProfileChangedEventArgs e)
    {
        if (!DeveloperProfileManager.DevDebug) return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => DeveloperProfileManager_OnCurrentProfileChanged(sender, e));
            return;
        }

        if (e.ProfilesUpdated)
            RefreshDeveloperProfilesMenuEntries();
        else
            UpdateDeveloperProfileMenuChecks();
    }
}
