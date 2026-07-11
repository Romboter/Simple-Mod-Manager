#nullable enable

using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ManageServerTargetsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ManageServerTargetsDialog(
            _serverTargetService,
            (target, password, hostKeyVerifier) =>
                ServerConnectionHelper.TestServerConnectionAsync(_serverTargetService, target, password, hostKeyVerifier),
            ShowHostKeyVerificationAsync)
        {
            Owner = this
        };
        dialog.ShowDialog();
        UpdateSyncToServerMenuState();
    }

    private async void SyncToServerMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var preflight = ServerSyncPreflight.Evaluate(
            _userConfiguration.IsActiveProfileServerProfile(),
            _userConfiguration.GetActiveServerTargetId(),
            _serverTargetService.GetTarget,
            _dataDirectory);

        if (preflight.Target is not { } target)
        {
            await _confirmationService.NotifyAsync(
                    preflight.ErrorMessage!,
                    "Sync to Server",
                    MapPreflightSeverity(preflight.Icon))
                .ConfigureAwait(true);
            return;
        }

        // Wrap the host key verifier to store trusted fingerprints
        var wrappedHostKeyVerifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _serverTargetService, target, ShowHostKeyVerificationAsync);

        var viewModel = new SyncToServerDialogViewModel(
            target,
            _dataDirectory!,
            _serverTargetService,
            _syncEngine,
            ServerConnectionHelper.CreateSftpClientWrapper,
            wrappedHostKeyVerifier,
            new ConfirmationService());

        var dialog = new SyncToServerDialog(viewModel)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private static DialogSeverity MapPreflightSeverity(MessageBoxImage icon) => icon switch
    {
        MessageBoxImage.Warning => DialogSeverity.Warning,
        MessageBoxImage.Error => DialogSeverity.Error,
        MessageBoxImage.Question => DialogSeverity.Question,
        _ => DialogSeverity.Information
    };

    private void UpdateSyncToServerMenuState()
    {
        SyncToServerMenuItem.IsEnabled = ServerSyncPreflight.CanSyncToServer(
            _userConfiguration.IsActiveProfileServerProfile(),
            _userConfiguration.GetActiveServerTargetId());
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
