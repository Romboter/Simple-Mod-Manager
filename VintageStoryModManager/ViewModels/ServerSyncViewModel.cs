using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Bound view-model for the server-sync menu items (Enable server options / Server Targets /
///     Sync to Server). Dialogs go through IConfirmationService/IDialogLauncher; the
///     selection-button-refresh side effect that belongs to the window goes through the injected
///     callback, matching SettingsMenuViewModel's callback-injection pattern.
/// </summary>
public sealed partial class ServerSyncViewModel : ObservableObject
{
    private readonly UserConfigurationService _userConfiguration;
    private readonly ServerTargetService _serverTargetService;
    private readonly SyncEngine _syncEngine;
    private readonly IConfirmationService _confirmationService;
    private readonly IDialogLauncher _dialogLauncher;
    private readonly Func<string, HostKeyVerificationResult, Task<bool>> _hostKeyVerifier;
    private readonly Func<string?> _dataDirectoryProvider;
    private readonly Action<bool> _onServerOptionsEnabledChanged;

    [ObservableProperty]
    private bool _isServerOptionsEnabled;

    public ServerSyncViewModel(
        UserConfigurationService userConfiguration,
        ServerTargetService serverTargetService,
        SyncEngine syncEngine,
        IConfirmationService confirmationService,
        IDialogLauncher dialogLauncher,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier,
        Func<string?> dataDirectoryProvider,
        Action<bool> onServerOptionsEnabledChanged)
    {
        _userConfiguration = userConfiguration ?? throw new ArgumentNullException(nameof(userConfiguration));
        _serverTargetService = serverTargetService ?? throw new ArgumentNullException(nameof(serverTargetService));
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _dialogLauncher = dialogLauncher ?? throw new ArgumentNullException(nameof(dialogLauncher));
        _hostKeyVerifier = hostKeyVerifier ?? throw new ArgumentNullException(nameof(hostKeyVerifier));
        _dataDirectoryProvider = dataDirectoryProvider ?? throw new ArgumentNullException(nameof(dataDirectoryProvider));
        _onServerOptionsEnabledChanged = onServerOptionsEnabledChanged ?? throw new ArgumentNullException(nameof(onServerOptionsEnabledChanged));

        _isServerOptionsEnabled = _userConfiguration.EnableServerOptions;
    }

    partial void OnIsServerOptionsEnabledChanged(bool value)
    {
        _userConfiguration.SetEnableServerOptions(value);
        _onServerOptionsEnabledChanged(value);
    }

    public bool CanSyncToServer() => ServerSyncPreflight.CanSyncToServer(
        _userConfiguration.IsActiveProfileServerProfile(),
        _userConfiguration.GetActiveServerTargetId());

    public void RefreshSyncAvailability() => SyncToServerCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ManageServerTargets()
    {
        var dialog = new ManageServerTargetsDialog(
            _serverTargetService,
            (target, password, hostKeyVerifier) =>
                ServerConnectionHelper.TestServerConnectionAsync(_serverTargetService, target, password, hostKeyVerifier),
            _hostKeyVerifier);

        _dialogLauncher.ShowDialog(dialog);
        RefreshSyncAvailability();
    }

    [RelayCommand(CanExecute = nameof(CanSyncToServer))]
    private async Task SyncToServerAsync()
    {
        var dataDirectory = _dataDirectoryProvider();

        var preflight = ServerSyncPreflight.Evaluate(
            _userConfiguration.IsActiveProfileServerProfile(),
            _userConfiguration.GetActiveServerTargetId(),
            _serverTargetService.GetTarget,
            dataDirectory);

        if (preflight.Target is not { } target)
        {
            await _confirmationService.NotifyAsync(
                    preflight.ErrorMessage!,
                    "Sync to Server",
                    MapPreflightSeverity(preflight.Icon))
                .ConfigureAwait(true);
            return;
        }

        var wrappedHostKeyVerifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _serverTargetService, target, _hostKeyVerifier);

        var viewModel = new SyncToServerDialogViewModel(
            target,
            dataDirectory!,
            _serverTargetService,
            _syncEngine,
            ServerConnectionHelper.CreateSftpClientWrapper,
            wrappedHostKeyVerifier,
            new ConfirmationService());

        var dialog = new SyncToServerDialog(viewModel);
        _dialogLauncher.ShowDialog(dialog);
    }

    private static DialogSeverity MapPreflightSeverity(MessageBoxImage icon) => icon switch
    {
        MessageBoxImage.Warning => DialogSeverity.Warning,
        MessageBoxImage.Error => DialogSeverity.Error,
        MessageBoxImage.Question => DialogSeverity.Question,
        _ => DialogSeverity.Information
    };
}
