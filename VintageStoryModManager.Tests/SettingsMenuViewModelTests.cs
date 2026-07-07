using System.IO;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class SettingsMenuViewModelTests
{
    private sealed class FakeUserConfiguration : IUserConfigurationService
    {
        public bool DisableAutoRefresh { get; set; }
        public bool DisableAutoRefreshWarningAcknowledged { get; set; }
        public void SetDisableAutoRefreshWarningAcknowledged(bool value) => DisableAutoRefreshWarningAcknowledged = value;
        public void SetDisableAutoRefresh(bool value) => DisableAutoRefresh = value;

        public bool AutomaticDataBackupsEnabled { get; set; }
        public bool AutomaticDataBackupsWarningAcknowledged { get; set; }
        public void SetAutomaticDataBackupsWarningAcknowledged(bool value) => AutomaticDataBackupsWarningAcknowledged = value;
        public void SetAutomaticDataBackupsEnabled(bool value) => AutomaticDataBackupsEnabled = value;

        public bool DisableInternetAccess { get; set; }
        public void SetDisableInternetAccess(bool value) => DisableInternetAccess = value;

        public ModlistAutoLoadBehavior ModlistAutoLoadBehavior { get; set; } = ModlistAutoLoadBehavior.Prompt;
        public void SetModlistAutoLoadBehavior(ModlistAutoLoadBehavior value) => ModlistAutoLoadBehavior = value;

        public bool UseFasterThumbnails { get; set; }
        public void SetUseFasterThumbnails(bool value) => UseFasterThumbnails = value;

        public bool CacheAllVersionsLocally { get; set; }
        public void SetCacheAllVersionsLocally(bool value) => CacheAllVersionsLocally = value;

        public bool RequireExactVsVersionMatch { get; set; }
        public void SetRequireExactVsVersionMatch(bool value) => RequireExactVsVersionMatch = value;

        public bool LogModUpdates { get; set; }
        public void SetLogModUpdates(bool value) => LogModUpdates = value;

        public bool LogModInstalls { get; set; }
        public void SetLogModInstalls(bool value) => LogModInstalls = value;

        public bool LogModDeletions { get; set; }
        public void SetLogModDeletions(bool value) => LogModDeletions = value;

        public bool LogAppLaunchAndExit { get; set; }
        public void SetLogAppLaunchAndExit(bool value) => LogAppLaunchAndExit = value;

        public bool LogErrorsAndExceptions { get; set; }
        public void SetLogErrorsAndExceptions(bool value) => LogErrorsAndExceptions = value;
    }

    private sealed class FakeConfirmation : IConfirmationService
    {
        public bool ConfirmAnswer { get; set; }
        public int ConfirmCalls { get; private set; }
        public int NotifyCalls { get; private set; }
        public List<(string Message, string Title)> NotifyMessages { get; } = new();

        public Task<bool> ConfirmAsync(string message, string title)
        {
            ConfirmCalls++;
            return Task.FromResult(ConfirmAnswer);
        }

        public Task NotifyAsync(string message, string title)
        {
            NotifyCalls++;
            NotifyMessages.Add((message, title));
            return Task.CompletedTask;
        }
    }

    private sealed record Fixture(
        SettingsMenuViewModel ViewModel,
        FakeUserConfiguration Configuration,
        FakeConfirmation Confirmation,
        List<bool> AutoRefreshChanges,
        List<bool> InternetAccessChanges,
        List<bool> ErrorLoggingChanges);

    private static Fixture CreateFixture()
    {
        var configuration = new FakeUserConfiguration();
        var confirmation = new FakeConfirmation();
        var autoRefreshChanges = new List<bool>();
        var internetAccessChangeCount = new List<bool>();
        var errorLoggingChanges = new List<bool>();

        string? dataDirectory = null;

        var viewModel = new SettingsMenuViewModel(
            configuration,
            confirmation,
            () => dataDirectory,
            disable => autoRefreshChanges.Add(disable),
            () => internetAccessChangeCount.Add(true),
            () => errorLoggingChanges.Add(true));

        var fixture = new Fixture(
            viewModel,
            configuration,
            confirmation,
            autoRefreshChanges,
            internetAccessChangeCount,
            errorLoggingChanges);

        return fixture;
    }

    // Helper to create fixture with a mutable data directory captured by the provider.
    private static (SettingsMenuViewModel ViewModel, FakeUserConfiguration Configuration, FakeConfirmation Confirmation, Action<string?> SetDataDirectory)
        CreateFixtureWithDataDirectory()
    {
        var configuration = new FakeUserConfiguration();
        var confirmation = new FakeConfirmation();
        string? dataDirectory = null;

        var viewModel = new SettingsMenuViewModel(
            configuration,
            confirmation,
            () => dataDirectory,
            _ => { },
            () => { },
            () => { });

        return (viewModel, configuration, confirmation, value => dataDirectory = value);
    }

    [Fact]
    public async Task DisableAutoRefresh_FirstEnable_Declined_NothingChanges()
    {
        var fixture = CreateFixture();
        fixture.Confirmation.ConfirmAnswer = false;

        await fixture.ViewModel.ToggleDisableAutoRefreshCommand.ExecuteAsync(null);

        Assert.False(fixture.ViewModel.DisableAutoRefresh);
        Assert.False(fixture.Configuration.DisableAutoRefresh);
        Assert.False(fixture.Configuration.DisableAutoRefreshWarningAcknowledged);
        Assert.Empty(fixture.AutoRefreshChanges);
    }

    [Fact]
    public async Task DisableAutoRefresh_FirstEnable_Accepted()
    {
        var fixture = CreateFixture();
        fixture.Confirmation.ConfirmAnswer = true;

        await fixture.ViewModel.ToggleDisableAutoRefreshCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.DisableAutoRefresh);
        Assert.True(fixture.Configuration.DisableAutoRefresh);
        Assert.True(fixture.Configuration.DisableAutoRefreshWarningAcknowledged);
        Assert.Equal(new[] { true }, fixture.AutoRefreshChanges);
    }

    [Fact]
    public async Task DisableAutoRefresh_AlreadyAcknowledged_NoPrompt()
    {
        var fixture = CreateFixture();
        fixture.Configuration.DisableAutoRefreshWarningAcknowledged = true;

        await fixture.ViewModel.ToggleDisableAutoRefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.Confirmation.ConfirmCalls);
        Assert.True(fixture.Configuration.DisableAutoRefresh);
        Assert.True(fixture.ViewModel.DisableAutoRefresh);
    }

    [Fact]
    public async Task DisableAutoRefresh_Disable_NoPrompt()
    {
        var fixture = CreateFixture();
        fixture.Configuration.DisableAutoRefreshWarningAcknowledged = true;
        await fixture.ViewModel.ToggleDisableAutoRefreshCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.DisableAutoRefresh);

        await fixture.ViewModel.ToggleDisableAutoRefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.Confirmation.ConfirmCalls);
        Assert.False(fixture.Configuration.DisableAutoRefresh);
        Assert.False(fixture.ViewModel.DisableAutoRefresh);
        Assert.Equal(new[] { true, false }, fixture.AutoRefreshChanges);
    }

    [Fact]
    public async Task Backups_InvalidDataDir_RevertsWithNotify()
    {
        var (viewModel, configuration, confirmation, _) = CreateFixtureWithDataDirectory();

        await viewModel.ToggleAutomaticDataBackupsCommand.ExecuteAsync(null);

        Assert.Equal(1, confirmation.NotifyCalls);
        Assert.False(configuration.AutomaticDataBackupsEnabled);
        Assert.False(viewModel.AutomaticDataBackupsEnabled);
    }

    [Fact]
    public async Task Backups_FirstEnable_NotifiesOnce_SetsAcknowledged()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var (viewModel, configuration, confirmation, setDataDirectory) = CreateFixtureWithDataDirectory();
            setDataDirectory(tempDir.FullName);

            await viewModel.ToggleAutomaticDataBackupsCommand.ExecuteAsync(null);

            Assert.Equal(1, confirmation.NotifyCalls);
            Assert.True(configuration.AutomaticDataBackupsWarningAcknowledged);
            Assert.True(configuration.AutomaticDataBackupsEnabled);
            Assert.True(viewModel.AutomaticDataBackupsEnabled);

            // Second cycle: disable, then re-enable - already acknowledged, so no additional notify.
            await viewModel.ToggleAutomaticDataBackupsCommand.ExecuteAsync(null);
            Assert.False(viewModel.AutomaticDataBackupsEnabled);

            await viewModel.ToggleAutomaticDataBackupsCommand.ExecuteAsync(null);
            Assert.True(viewModel.AutomaticDataBackupsEnabled);
            Assert.Equal(1, confirmation.NotifyCalls);
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    [Fact]
    public void ModlistPair_MutuallyExclusive()
    {
        var fixture = CreateFixture();

        fixture.ViewModel.AlwaysClearModlists = true;
        Assert.Equal(ModlistAutoLoadBehavior.Replace, fixture.Configuration.ModlistAutoLoadBehavior);
        Assert.False(fixture.ViewModel.AlwaysAddModlists);

        fixture.ViewModel.AlwaysAddModlists = true;
        Assert.Equal(ModlistAutoLoadBehavior.Add, fixture.Configuration.ModlistAutoLoadBehavior);
        Assert.False(fixture.ViewModel.AlwaysClearModlists);

        fixture.ViewModel.AlwaysAddModlists = false;
        Assert.Equal(ModlistAutoLoadBehavior.Prompt, fixture.Configuration.ModlistAutoLoadBehavior);
    }

    [Fact]
    public void PlainToggle_WritesConfig()
    {
        var fixture = CreateFixture();

        fixture.ViewModel.LogModUpdates = true;
        Assert.True(fixture.Configuration.LogModUpdates);

        fixture.ViewModel.LogErrorsAndExceptions = true;
        Assert.True(fixture.Configuration.LogErrorsAndExceptions);
        Assert.Single(fixture.ErrorLoggingChanges);
    }

    [Fact]
    public void InternetToggle_FiresStateCallback()
    {
        var priorState = InternetAccessManager.IsInternetAccessDisabled;
        try
        {
            var fixture = CreateFixture();

            fixture.ViewModel.DisableInternetAccess = true;

            Assert.Single(fixture.InternetAccessChanges);
            Assert.True(fixture.Configuration.DisableInternetAccess);
            Assert.True(InternetAccessManager.IsInternetAccessDisabled);
        }
        finally
        {
            InternetAccessManager.SetInternetAccessDisabled(priorState);
        }
    }
}
