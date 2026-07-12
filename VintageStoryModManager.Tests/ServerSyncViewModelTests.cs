using System.IO;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ServerSyncViewModelTests
{
    // ServerSyncViewModel takes the concrete UserConfigurationService (no interface exists for
    // the members it needs), and UserConfigurationService only has a parameterless constructor
    // that reads/writes real user config. Same accepted trade-off CloudWorkflowCoordinatorTests
    // already lives with (new UserConfigurationService() there too) - not fixed here, out of scope.
    private sealed class FakeConfirmationService : IConfirmationService
    {
        public int NotifyCalls { get; private set; }
        public List<(string Message, string Title, DialogSeverity Severity)> NotifyMessages { get; } = new();

        public Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null) => Task.FromResult(false);

        public Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information)
        {
            NotifyCalls++;
            NotifyMessages.Add((message, title, severity));
            return Task.CompletedTask;
        }

        public Task<ThreeWayConfirmResult> ConfirmThreeWayAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? yesText = null, string? noText = null,
            SuppressibleConfirmOption? suppressOption = null) =>
            Task.FromResult(ThreeWayConfirmResult.Cancel);

        public Task<bool> ConfirmOkCancelAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? okText = null, string? cancelText = null) => Task.FromResult(false);
    }

    private sealed class FakeDialogLauncher : IDialogLauncher
    {
        public int ShowDialogCalls { get; private set; }

        public bool? ShowDialog(Window dialog)
        {
            ShowDialogCalls++;
            return null;
        }
    }

    private static (ServerSyncViewModel ViewModel, UserConfigurationService Configuration,
        ServerTargetService TargetService, FakeConfirmationService Confirmation,
        FakeDialogLauncher DialogLauncher, List<bool> ServerOptionsChanges) CreateFixture()
    {
        var configuration = new UserConfigurationService();
        var targetService = new ServerTargetService(
            Path.Combine(Path.GetTempPath(), "SVSM-Tests-" + Guid.NewGuid()));
        var confirmation = new FakeConfirmationService();
        var dialogLauncher = new FakeDialogLauncher();
        var serverOptionsChanges = new List<bool>();

        var viewModel = new ServerSyncViewModel(
            configuration,
            targetService,
            new SyncEngine(),
            confirmation,
            dialogLauncher,
            (_, _) => Task.FromResult(true),
            () => null,
            value => serverOptionsChanges.Add(value));

        return (viewModel, configuration, targetService, confirmation, dialogLauncher, serverOptionsChanges);
    }

    [Fact]
    public void Constructor_InitializesIsServerOptionsEnabled_FromConfiguration()
    {
        var configuration = new UserConfigurationService();
        configuration.SetEnableServerOptions(true);

        var viewModel = new ServerSyncViewModel(
            configuration,
            new ServerTargetService(Path.Combine(Path.GetTempPath(), "SVSM-Tests-" + Guid.NewGuid())),
            new SyncEngine(),
            new FakeConfirmationService(),
            new FakeDialogLauncher(),
            (_, _) => Task.FromResult(true),
            () => null,
            _ => { });

        Assert.True(viewModel.IsServerOptionsEnabled);

        configuration.SetEnableServerOptions(false);
    }

    [Fact]
    public void SettingIsServerOptionsEnabled_PersistsAndInvokesCallback()
    {
        var (viewModel, configuration, _, _, _, serverOptionsChanges) = CreateFixture();

        // UserConfigurationService.Save() is a no-op until EnablePersistence() is called (only
        // MainWindow.Startup.cs/FirebaseAnonymousAuthenticator do that), so this test's
        // SetEnableServerOptions calls never touch disk - but Load() in the constructor still
        // reads whatever this machine's real, shared user config last had persisted from actual
        // app usage. Toggle to the opposite of the current value so the test is deterministic
        // regardless of that starting value.
        var expected = !viewModel.IsServerOptionsEnabled;

        viewModel.IsServerOptionsEnabled = expected;

        Assert.Equal(expected, configuration.EnableServerOptions);
        Assert.Equal(new[] { expected }, serverOptionsChanges);
    }

    [Fact]
    public void CanSyncToServer_ReflectsActiveProfile()
    {
        var (viewModel, configuration, _, _, _, _) = CreateFixture();

        configuration.SetActiveProfileServerSettings(ProfileType.Local, null);
        Assert.False(viewModel.CanSyncToServer());

        configuration.SetActiveProfileServerSettings(ProfileType.Server, "target-1");
        Assert.True(viewModel.CanSyncToServer());

        configuration.SetActiveProfileServerSettings(ProfileType.Local, null);
    }

    [Fact]
    public async Task SyncToServerAsync_WhenNotServerProfile_NotifiesAndDoesNotOpenDialog()
    {
        var (viewModel, configuration, _, confirmation, dialogLauncher, _) = CreateFixture();
        configuration.SetActiveProfileServerSettings(ProfileType.Local, null);

        await viewModel.SyncToServerCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmation.NotifyCalls);
        Assert.Equal("Sync to Server", confirmation.NotifyMessages[0].Title);
        Assert.Equal(0, dialogLauncher.ShowDialogCalls);
    }

    [Fact]
    public void RefreshSyncAvailability_UpdatesSyncToServerCanExecute()
    {
        var (viewModel, configuration, _, _, _, _) = CreateFixture();
        configuration.SetActiveProfileServerSettings(ProfileType.Local, null);

        Assert.False(viewModel.SyncToServerCommand.CanExecute(null));

        configuration.SetActiveProfileServerSettings(ProfileType.Server, "target-1");
        viewModel.RefreshSyncAvailability();

        Assert.True(viewModel.SyncToServerCommand.CanExecute(null));

        configuration.SetActiveProfileServerSettings(ProfileType.Local, null);
    }
}
