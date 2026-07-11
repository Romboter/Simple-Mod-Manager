#nullable enable

using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async Task OnActiveGameProfileChangedAsync()
    {
        TryInitializePaths();
        RefreshDeveloperProfilesMenuEntries();
        UpdateGameVersionMenuItem(VintageStoryVersionLocator.GetInstalledVersion(_gameDirectory));
        await ReloadViewModelAsync();
        UpdateActiveGameProfileDisplay();
        UpdateSyncToServerMenuState();
    }

    private void GameProfilesMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        RefreshGameProfileMenuItems();
        UpdateGameProfileMenuChecks();
    }

    private async void CreateGameProfileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_userConfiguration.GameProfileCreationWarningAcknowledged)
        {
            var confirmation = await _confirmationService.ConfirmOkCancelAsync(
                    "Game Profiles are specifically made to manage different Vintage Story installations, using different Data and Game folders. If you are looking for a way to easily switch between mod lists, use Modlists to swap between different mod sets. This dialog will not be shown again.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);

            if (!confirmation) return;

            _userConfiguration.SetGameProfileCreationWarningAcknowledged(true);
        }

        var dialog = new GameProfileDialog(
            this,
            _serverTargetService,
            (target, password, hostKeyVerifier) =>
                ServerConnectionHelper.TestServerConnectionAsync(_serverTargetService, target, password, hostKeyVerifier),
            ShowHostKeyVerificationAsync,
            _userConfiguration.EnableServerOptions);
        var result = dialog.ShowDialog();
        if (result != true) return;

        var profileName = dialog.ProfileName;

        if (!_userConfiguration.TryCreateGameProfile(profileName, out var normalizedName, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                await _confirmationService.NotifyAsync(
                    errorMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                    .ConfigureAwait(true);

            return;
        }

        if (normalizedName is not null)
        {
            _userConfiguration.TrySetActiveGameProfile(normalizedName);

            // Set profile type and server target if this is a server profile
            _userConfiguration.SetActiveProfileType(dialog.SelectedProfileType);
            if (dialog.SelectedProfileType == ProfileType.Server && dialog.SelectedServerTargetId != null)
            {
                _userConfiguration.SetActiveServerTargetId(dialog.SelectedServerTargetId);
            }
        }

        await OnActiveGameProfileChangedAsync().ConfigureAwait(true);
        RefreshGameProfileMenuItems();
        UpdateGameProfileMenuChecks();
    }

    private async void EditGameProfileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var profileName = _userConfiguration.ActiveGameProfileName;
        if (string.IsNullOrEmpty(profileName))
        {
            await _confirmationService.NotifyAsync(
                "No active profile to edit.",
                "Simple VS Manager",
                DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var currentProfileType = _userConfiguration.GetActiveProfileType();
        var currentServerTargetId = _userConfiguration.GetActiveServerTargetId();

        var dialog = new EditGameProfileDialog(
            this,
            profileName,
            currentProfileType,
            currentServerTargetId,
            _serverTargetService,
            (target, password, hostKeyVerifier) =>
                ServerConnectionHelper.TestServerConnectionAsync(_serverTargetService, target, password, hostKeyVerifier),
            ShowHostKeyVerificationAsync,
            _userConfiguration.EnableServerOptions);

        var result = dialog.ShowDialog();
        if (result != true) return;

        // Update profile type and server target
        _userConfiguration.SetActiveProfileType(dialog.SelectedProfileType);
        _userConfiguration.SetActiveServerTargetId(dialog.SelectedServerTargetId);

        UpdateSyncToServerMenuState();
    }

    private async void DeleteGameProfileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var profiles = _userConfiguration.GetGameProfileNames();
        if (profiles.Count == 0) return;

        var activeProfile = _userConfiguration.ActiveGameProfileName;
        var dialog = new DeleteGameProfilesDialog(profiles, activeProfile);
        var result = dialog.ShowDialog();
        if (result != true) return;

        var selectedProfiles = dialog.SelectedProfileNames;
        if (selectedProfiles.Count == 0) return;

        if (!_userConfiguration.TryDeleteGameProfiles(selectedProfiles, out var errorMessage,
                out var activeProfileChanged))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                await _confirmationService.NotifyAsync(
                    errorMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                    .ConfigureAwait(true);

            return;
        }

        if (activeProfileChanged) await OnActiveGameProfileChangedAsync().ConfigureAwait(true);

        RefreshGameProfileMenuItems();
        UpdateGameProfileMenuChecks();
    }

    private async void GameProfileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string profileName) return;

        if (string.Equals(profileName, _userConfiguration.ActiveGameProfileName, StringComparison.OrdinalIgnoreCase))
        {
            menuItem.IsChecked = true;
            return;
        }

        if (!_userConfiguration.TrySetActiveGameProfile(profileName)) return;

        await OnActiveGameProfileChangedAsync().ConfigureAwait(true);
        UpdateGameProfileMenuChecks();
    }

    private void RefreshGameProfileMenuItems()
    {
        if (GameProfilesMenuItem is null || CreateGameProfileMenuItem is null) return;

        foreach (var item in _gameProfileMenuItems) item.Click -= GameProfileMenuItem_OnClick;

        _gameProfileMenuItems.Clear();

        GameProfilesMenuItem.Items.Clear();
        GameProfilesMenuItem.Items.Add(CreateGameProfileMenuItem);

        if (EditGameProfileMenuItem is not null) GameProfilesMenuItem.Items.Add(EditGameProfileMenuItem);

        if (DeleteGameProfileMenuItem is not null) GameProfilesMenuItem.Items.Add(DeleteGameProfileMenuItem);

        var profiles = _userConfiguration.GetGameProfileNames();
        if (DeleteGameProfileMenuItem is not null)
            DeleteGameProfileMenuItem.IsEnabled = profiles.Any(name => !_userConfiguration.IsDefaultGameProfile(name));

        if (profiles.Count > 0) GameProfilesMenuItem.Items.Add(new Separator());

        var activeName = _userConfiguration.ActiveGameProfileName;

        foreach (var profileName in profiles)
        {
            var menuItem = new MenuItem
            {
                Header = profileName,
                Tag = profileName,
                IsCheckable = true,
                Height = 35,
                IsChecked = string.Equals(profileName, activeName, StringComparison.OrdinalIgnoreCase)
            };

            menuItem.Click += GameProfileMenuItem_OnClick;
            GameProfilesMenuItem.Items.Add(menuItem);
            _gameProfileMenuItems.Add(menuItem);
        }

        UpdateActiveGameProfileDisplay();
    }

    private void UpdateGameProfileMenuChecks()
    {
        var activeName = _userConfiguration.ActiveGameProfileName;

        foreach (var item in _gameProfileMenuItems)
            if (item.Tag is string profileName)
                item.IsChecked = string.Equals(profileName, activeName, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateActiveGameProfileDisplay()
    {
        if (ActiveGameProfileTextBlock is null) return;

        var profiles = _userConfiguration.GetGameProfileNames();
        var hasAdditionalProfiles = profiles.Any(name => !_userConfiguration.IsDefaultGameProfile(name));
        if (!hasAdditionalProfiles)
        {
            ActiveGameProfileTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        var activeName = _userConfiguration.ActiveGameProfileName;
        if (string.IsNullOrWhiteSpace(activeName)) activeName = UserConfigurationService.DefaultProfileName;

        ActiveGameProfileTextBlock.Text = $"Profile: {activeName}";
        ActiveGameProfileTextBlock.Visibility = Visibility.Visible;
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
