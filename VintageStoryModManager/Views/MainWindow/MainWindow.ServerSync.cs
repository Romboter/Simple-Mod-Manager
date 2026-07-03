#nullable enable

using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ManageServerTargetsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ManageServerTargetsDialog(
            _serverTargetService,
            TestServerConnectionAsync,
            ShowHostKeyVerificationAsync)
        {
            Owner = this
        };
        dialog.ShowDialog();
        UpdateSyncToServerMenuState();
    }

    private void SyncToServerMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_userConfiguration.IsActiveProfileServerProfile())
        {
            WpfMessageBox.Show("This feature is only available for Server profiles.\n\nTo use this feature, create a new profile and set its type to 'Server'.",
                "Sync to Server", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var serverTargetId = _userConfiguration.GetActiveServerTargetId();
        if (string.IsNullOrEmpty(serverTargetId))
        {
            WpfMessageBox.Show("No server target is configured for this profile.",
                "Sync to Server", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var target = _serverTargetService.GetTarget(serverTargetId);
        if (target == null)
        {
            WpfMessageBox.Show("The configured server target was not found. It may have been deleted.",
                "Sync to Server", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            WpfMessageBox.Show("No data directory is configured for this profile.",
                "Sync to Server", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Wrap the host key verifier to store trusted fingerprints
        var wrappedHostKeyVerifier = CreateHostKeyVerifierWithStorage()(target, ShowHostKeyVerificationAsync);

        var viewModel = new SyncToServerDialogViewModel(
            target,
            _dataDirectory,
            _serverTargetService,
            _syncEngine,
            CreateSftpClientWrapper,
            wrappedHostKeyVerifier,
            new ConfirmationService());

        var dialog = new SyncToServerDialog(viewModel)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void UpdateSyncToServerMenuState()
    {
        var isServerProfile = _userConfiguration.IsActiveProfileServerProfile();
        var hasServerTarget = !string.IsNullOrEmpty(_userConfiguration.GetActiveServerTargetId());
        SyncToServerMenuItem.IsEnabled = isServerProfile && hasServerTarget;
    }

    private void UpdateServerOptionsState(bool isEnabled)
    {
        if (EnableServerOptionsMenuItem is not null) EnableServerOptionsMenuItem.IsChecked = isEnabled;

        // Control visibility of server-related menu items
        var visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (ManageServerTargetsMenuItem is not null) ManageServerTargetsMenuItem.Visibility = visibility;
        if (SyncToServerMenuItem is not null) SyncToServerMenuItem.Visibility = visibility;
        if (ServerOptionsSeparator1 is not null) ServerOptionsSeparator1.Visibility = visibility;
        if (ServerOptionsSeparator2 is not null) ServerOptionsSeparator2.Visibility = visibility;

        var singleSelection = _selectedMods.Count == 1 ? _selectedMods[0] : null;
        UpdateSelectedModCopyForServerButton(isEnabled ? singleSelection : null);
    }

    private void EnableServerOptionsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetEnableServerOptions(menuItem.IsChecked);
        var isEnabled = _userConfiguration.EnableServerOptions;
        UpdateServerOptionsState(isEnabled);
    }
}
