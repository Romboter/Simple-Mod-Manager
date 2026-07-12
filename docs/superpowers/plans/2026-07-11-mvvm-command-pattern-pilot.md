# MVVM Command Pattern Pilot (Area H, Server Sync) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the `[RelayCommand]`/`[ObservableProperty]` MVVM command pattern (design doc: `docs/superpowers/specs/2026-07-11-mvvm-command-pattern-design.md`) on one small real area — Server Sync — converting its XAML `Click=` handlers to `Command` bindings against a new `ServerSyncViewModel`, plus a new `IDialogLauncher` seam for opening dialog `Window`s from a ViewModel.

**Architecture:** New `ServerSyncViewModel` (partial class, `CommunityToolkit.Mvvm` source generators) owns server-sync state and commands. `MainWindow` stays the manual composition root — constructs the ViewModel, exposes it as a property, implements the new `IDialogLauncher` interface itself (it's the one thing that actually knows the owner `Window`). XAML binds directly to `ServerSync.*` via `RelativeSource AncestorType=Window`, matching the existing `SettingsMenu.*` binding pattern already in `MainWindow.xaml`.

**Tech Stack:** .NET 8, WPF, `CommunityToolkit.Mvvm` 8.4.0 (already referenced, source generators only — no new dependency), xUnit.

## Global Constraints

- Zero errors, zero warnings on Release build (CLAUDE.md rule).
- Move, don't rewrite: preserve exact existing behavior/messages/control names except where this plan explicitly changes wiring.
- No DI container — manual construction in `MainWindow`'s constructor, per the design doc's Section D.
- `dotnet format --include <files>` then the IDE0005 unused-usings check, per CLAUDE.md's extraction workflow, before each commit that touches production code.

## Scope note (discovered during planning, not in the original design doc)

The design doc's Section E table listed 4 handlers for the pilot. File inspection during planning found that the 4th, `SelectedModCopyForServerButton_OnClick`, has its `IsEnabled`/`Visibility`/`DataContext` driven imperatively by `MainWindow.ModGridSelection.cs`'s `UpdateSelectedModCopyForServerButton` — the exact `UpdateSelectedMod*Button` cross-area coupling CLAUDE.md's roadmap already flags as "analyzer candidate #1," shared with areas A and F. Converting that 4th handler to a `Command` binding would fight that imperative `IsEnabled` push (WPF recomputes a bound button's `IsEnabled` from `CanExecute` on every `CommandManager.RequerySuggested`, conflicting with a parallel imperative write) and would require touching Area A's selection-button-refresh mechanism — out of scope for a pilot that's supposed to stay small and area-local.

**This plan converts 3 handlers, not 4:** `EnableServerOptionsMenuItem_OnClick`, `ManageServerTargetsMenuItem_OnClick`, `SyncToServerMenuItem_OnClick`. `SelectedModCopyForServerButton_OnClick` stays as today's code-behind handler; converting it is deferred until Area A's selection-button-refresh gets its own MVVM pass. This still proves every pattern the design doc needs: a two-way bound toggle with a persisted side effect, a dialog-window launch via the new `IDialogLauncher` seam, and an async command with `CanExecute` + preflight + confirmation-dialog notification.

One consequence: `EnableServerOptionsMenuItem`'s toggle still needs to drive `UpdateSelectedModCopyForServerButton` (today's `UpdateServerOptionsState` does this as its last line) — this plan preserves that via an injected `Action<bool>` callback into `ServerSyncViewModel`, the same callback-injection pattern `SettingsMenuViewModel` already uses for its own window-owned side effects (`onAutoRefreshDisabledChanged`, etc.). No Area A code moves; `MainWindow` still owns and calls `UpdateSelectedModCopyForServerButton` exactly as it does today.

Also out of scope for automated testing: the two dialog-launching commands (`ManageServerTargets`, and `SyncToServerAsync`'s preflight-success path) construct real WPF `Window`-derived dialogs (`ManageServerTargetsDialog`, `SyncToServerDialog`). No test in this codebase has ever constructed a live `Window` subclass in xunit, and this project has no WPF UI test harness (CLAUDE.md's Testing Notes / the project's own "no WPF GUI automation in this env" note). Rather than be the first to find out whether that works in this environment, this plan tests only the parts of `ServerSyncViewModel` that don't construct a `Window` (property/persistence/callback behavior, `CanExecute`, and the preflight-*failure* path, which returns before touching `IDialogLauncher`), and covers the dialog-launching paths via the manual smoke-test checklist in Task 5 — consistent with how every other `MainWindow.*.cs` partial in this codebase is verified.

---

### Task 1: `IDialogLauncher` seam

**Files:**
- Create: `VintageStoryModManager\Services\IDialogLauncher.cs`
- Modify: `VintageStoryModManager\Views\MainWindow\MainWindow.Core.cs:17` (class declaration), and add the implementing method near the other small owned-behavior methods in that file.

**Interfaces:**
- Produces: `IDialogLauncher.ShowDialog(Window dialog) : bool?` — Task 2's `ServerSyncViewModel` consumes this via constructor injection.

This task has no automated test — it's a one-line interface plus a trivial pass-through implementation (`dialog.Owner = this; return dialog.ShowDialog();`), the same class of "compiler-verified only" code the rest of `MainWindow.*.cs` already accepts (CLAUDE.md Testing Notes). Verification is the Release build in Step 3.

- [ ] **Step 1: Create the interface**

```csharp
using System.Windows;

namespace VintageStoryModManager.Services;

/// <summary>
///     Lets a ViewModel open a dialog Window without holding a direct reference to the
///     owner window. Implemented by MainWindow, the only type that actually knows the owner.
/// </summary>
public interface IDialogLauncher
{
    bool? ShowDialog(Window dialog);
}
```

Save as `VintageStoryModManager\Services\IDialogLauncher.cs`.

- [ ] **Step 2: Implement it on MainWindow**

In `VintageStoryModManager\Views\MainWindow\MainWindow.Core.cs`, change line 17 from:

```csharp
public partial class MainWindow : Window
```

to:

```csharp
public partial class MainWindow : Window, IDialogLauncher
```

Then add this method to the same file (anywhere among the other small methods, e.g. directly above the `public SettingsMenuViewModel SettingsMenu { get; }` property block near the end of the file):

```csharp
public bool? ShowDialog(Window dialog)
{
    ArgumentNullException.ThrowIfNull(dialog);
    dialog.Owner = this;
    return dialog.ShowDialog();
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build ./ImprovedModMenu.sln --configuration Release`
Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Format and check for stale usings**

Run:
```
dotnet format --include VintageStoryModManager/Services/IDialogLauncher.cs VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/Services/IDialogLauncher.cs VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
```
Expected: second command exits clean (no output, exit code 0).

- [ ] **Step 5: Commit**

```bash
git add VintageStoryModManager/Services/IDialogLauncher.cs VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs
git commit -m "feat: add IDialogLauncher seam for ViewModel-launched dialog windows"
```

---

### Task 2: `ServerSyncViewModel`

**Files:**
- Create: `VintageStoryModManager\ViewModels\ServerSyncViewModel.cs`
- Test: `VintageStoryModManager.Tests\ServerSyncViewModelTests.cs`

**Interfaces:**
- Consumes: `IDialogLauncher.ShowDialog(Window) : bool?` (Task 1). `IConfirmationService.NotifyAsync(string, string, DialogSeverity) : Task` (existing). `ServerSyncPreflight.Evaluate(bool, string?, Func<string, ServerTarget?>, string?) : ServerSyncPreflightResult` and `ServerSyncPreflight.CanSyncToServer(bool, string?) : bool` (existing, `internal static`, same assembly). `ServerConnectionHelper.CreateHostKeyVerifierWithStorage(...)`, `ServerConnectionHelper.CreateSftpClientWrapper(...)`, `ServerConnectionHelper.TestServerConnectionAsync(...)` (existing, `internal static`). `UserConfigurationService.EnableServerOptions : bool` (get), `.SetEnableServerOptions(bool)`, `.IsActiveProfileServerProfile() : bool`, `.GetActiveServerTargetId() : string?` (existing). `ServerTargetService.GetTarget : Func<string, ServerTarget?>` (existing). `ManageServerTargetsDialog(ServerTargetService, Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>>, Func<string, HostKeyVerificationResult, Task<bool>>)` and `SyncToServerDialog(SyncToServerDialogViewModel)` constructors (existing).
- Produces: `ServerSyncViewModel` with public `bool IsServerOptionsEnabled { get; set; }` (generated), `bool CanSyncToServer()`, `void RefreshSyncAvailability()`, `IRelayCommand ManageServerTargetsCommand` (generated), `IAsyncRelayCommand SyncToServerCommand` (generated) — Task 3 constructs this and binds `ServerSync` in `MainWindow`; Task 4's XAML binds directly to these member names.

- [ ] **Step 1: Write the failing tests**

Create `VintageStoryModManager.Tests\ServerSyncViewModelTests.cs`:

```csharp
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

        viewModel.IsServerOptionsEnabled = true;

        Assert.True(configuration.EnableServerOptions);
        Assert.Equal(new[] { true }, serverOptionsChanges);

        configuration.SetEnableServerOptions(false);
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
```

- [ ] **Step 2: Run tests to verify they fail (ServerSyncViewModel doesn't exist yet)**

Run: `dotnet test ./ImprovedModMenu.sln --filter FullyQualifiedName~ServerSyncViewModelTests`
Expected: build error — `ServerSyncViewModel`/`IDialogLauncher` usage doesn't resolve to a `VintageStoryModManager.ViewModels.ServerSyncViewModel` type (IDialogLauncher exists from Task 1, ServerSyncViewModel does not).

- [ ] **Step 3: Write the implementation**

Create `VintageStoryModManager\ViewModels\ServerSyncViewModel.cs`:

```csharp
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ./ImprovedModMenu.sln --filter FullyQualifiedName~ServerSyncViewModelTests`
Expected: PASS, 5/5.

- [ ] **Step 5: Format and check for stale usings**

Run:
```
dotnet format --include VintageStoryModManager/ViewModels/ServerSyncViewModel.cs VintageStoryModManager.Tests/ServerSyncViewModelTests.cs
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/ViewModels/ServerSyncViewModel.cs VintageStoryModManager.Tests/ServerSyncViewModelTests.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
```
Expected: second command exits clean.

- [ ] **Step 6: Build Release and run the full test suite**

Run: `dotnet build ./ImprovedModMenu.sln --configuration Release`
Expected: 0 errors, 0 warnings.

Run: `dotnet test ./ImprovedModMenu.sln --configuration Release`
Expected: all tests pass (previous total + 5).

- [ ] **Step 7: Commit**

```bash
git add VintageStoryModManager/ViewModels/ServerSyncViewModel.cs VintageStoryModManager.Tests/ServerSyncViewModelTests.cs
git commit -m "feat: add ServerSyncViewModel with RelayCommand-based server-sync commands"
```

---

### Task 3: Wire `ServerSyncViewModel` into `MainWindow`

**Files:**
- Modify: `VintageStoryModManager\Views\MainWindow\MainWindow.Core.cs`
- Modify: `VintageStoryModManager\Views\MainWindow\MainWindow.ServerSync.cs`
- Modify: `VintageStoryModManager\Views\MainWindow\MainWindow.Profiles.cs`

**Interfaces:**
- Consumes: `ServerSyncViewModel` (Task 2), `IDialogLauncher` implementation on `MainWindow` (Task 1).
- Produces: `MainWindow.ServerSync : ServerSyncViewModel` property — Task 4's XAML binds to `ServerSync.*` via `RelativeSource AncestorType=Window`, matching the existing `SettingsMenu.*` pattern already in `MainWindow.xaml`.

No new automated test — this task is pure composition/wiring in code-behind, verified by build + the existing `DialogSeamRegressionTests`-style compiler/behavior checks and the Task 5 smoke test.

- [ ] **Step 1: Construct `ServerSyncViewModel` in `MainWindow`'s constructor and expose it**

In `VintageStoryModManager\Views\MainWindow\MainWindow.Core.cs`, insert this right after the existing `_serverTargetService = new ServerTargetService(...)` line (currently line 322) and before `_modSelection = new ModGridSelectionService(...)` (currently line 323):

```csharp
        ServerSync = new ServerSyncViewModel(
            _userConfiguration,
            _serverTargetService,
            _syncEngine,
            _confirmationService,
            this,
            ShowHostKeyVerificationAsync,
            () => _dataDirectory,
            OnServerOptionsEnabledChanged);
```

Then add the property next to the existing `SettingsMenu`/`ThemeMenu` properties near the end of the file (after `public SettingsMenuViewModel SettingsMenu { get; }`):

```csharp
    public ServerSyncViewModel ServerSync { get; }
```

Then replace the two existing calls that this ViewModel now owns:

Replace (currently line 356):
```csharp
        UpdateServerOptionsState(_userConfiguration.EnableServerOptions);
```
with:
```csharp
        OnServerOptionsEnabledChanged(_userConfiguration.EnableServerOptions);
```

Replace (currently line 382):
```csharp
        UpdateSyncToServerMenuState();
```
with:
```csharp
        ServerSync.RefreshSyncAvailability();
```

- [ ] **Step 2: Trim `MainWindow.ServerSync.cs` down to the selection-button callback**

Replace the entire contents of `VintageStoryModManager\Views\MainWindow\MainWindow.ServerSync.cs` with:

```csharp
#nullable enable

using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void OnServerOptionsEnabledChanged(bool isEnabled)
    {
        var singleSelection = _selectedMods.Count == 1 ? _selectedMods[0] : null;
        UpdateSelectedModCopyForServerButton(isEnabled ? singleSelection : null);
    }
}
```

This deletes `ManageServerTargetsMenuItem_OnClick`, `SyncToServerMenuItem_OnClick`, `MapPreflightSeverity`, `UpdateSyncToServerMenuState`, `UpdateServerOptionsState`, and `EnableServerOptionsMenuItem_OnClick` — all now owned by `ServerSyncViewModel` or replaced by the one-line callback above. `using VintageStoryModManager.Services;` and `using VintageStoryModManager.Views.Dialogs;` are no longer needed in this file (moved to `ServerSyncViewModel.cs`); `using System.Windows;` and `using System.Windows.Controls;` are also no longer needed since `MenuItem`/`Visibility` are gone from this file.

- [ ] **Step 3: Update the two `Profiles.cs` call sites**

In `VintageStoryModManager\Views\MainWindow\MainWindow.Profiles.cs`, replace both occurrences of:
```csharp
        UpdateSyncToServerMenuState();
```
(at the end of `OnActiveGameProfileChangedAsync`, currently line 21, and at the end of the profile-type/server-target edit flow, currently line 120)
with:
```csharp
        ServerSync.RefreshSyncAvailability();
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build ./ImprovedModMenu.sln --configuration Release`
Expected: 0 errors, 0 warnings. (XAML still references the old `Click=` handlers at this point — Task 4 fixes that. If this build fails with "handler not found" errors from `MainWindow.xaml`, that's expected and resolved by Task 4; if it fails for any other reason, stop and investigate before proceeding.)

- [ ] **Step 5: Format and check for stale usings**

Run:
```
dotnet format --include VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs VintageStoryModManager/Views/MainWindow/MainWindow.ServerSync.cs VintageStoryModManager/Views/MainWindow/MainWindow.Profiles.cs
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs VintageStoryModManager/Views/MainWindow/MainWindow.ServerSync.cs VintageStoryModManager/Views/MainWindow/MainWindow.Profiles.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
```
Expected: second command exits clean.

- [ ] **Step 6: Commit**

```bash
git add VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs VintageStoryModManager/Views/MainWindow/MainWindow.ServerSync.cs VintageStoryModManager/Views/MainWindow/MainWindow.Profiles.cs
git commit -m "refactor: wire ServerSyncViewModel into MainWindow, retire UpdateServerOptionsState/UpdateSyncToServerMenuState"
```

---

### Task 4: XAML bindings

**Files:**
- Modify: `VintageStoryModManager\Views\MainWindow.xaml:166-178` (Server Targets / Sync to Server menu items + their separators)
- Modify: `VintageStoryModManager\Views\MainWindow.xaml:519-525` (Enable server options menu item)

**Interfaces:**
- Consumes: `ServerSyncViewModel`'s `IsServerOptionsEnabled`, `ManageServerTargetsCommand`, `SyncToServerCommand` (Task 2), exposed via `MainWindow.ServerSync` (Task 3).

No automated test (XAML binding correctness isn't xunit-testable in this codebase). Verified by build + Task 5's manual smoke test.

- [ ] **Step 1: Bind the "Enable server options" toggle**

In `VintageStoryModManager\Views\MainWindow.xaml`, replace (currently lines 519-525):

```xml
                        <MenuItem
                            x:Name="EnableServerOptionsMenuItem"
                            Padding="12,6"
                            Click="EnableServerOptionsMenuItem_OnClick"
                            Header="Enable server options"
                            IsCheckable="True"
                            Style="{StaticResource ToggleMenuItemStyle}" />
```

with:

```xml
                        <MenuItem
                            x:Name="EnableServerOptionsMenuItem"
                            Padding="12,6"
                            Header="Enable server options"
                            IsCheckable="True"
                            IsChecked="{Binding ServerSync.IsServerOptionsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}"
                            Style="{StaticResource ToggleMenuItemStyle}" />
```

- [ ] **Step 2: Bind the dependent controls' visibility and the two commands**

Replace (currently lines 166-178):

```xml
                        <Separator x:Name="ServerOptionsSeparator1" />
                        <MenuItem
                            x:Name="ManageServerTargetsMenuItem"
                            Height="35"
                            Click="ManageServerTargetsMenuItem_OnClick"
                            Header="Server Targets..." />
                        <MenuItem
                            x:Name="SyncToServerMenuItem"
                            Height="35"
                            Click="SyncToServerMenuItem_OnClick"
                            Header="Sync to Server..."
                            IsEnabled="False" />
                        <Separator x:Name="ServerOptionsSeparator2" />
```

with:

```xml
                        <Separator
                            x:Name="ServerOptionsSeparator1"
                            Visibility="{Binding ServerSync.IsServerOptionsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource IMM.BooleanToVisibilityConverter}}" />
                        <MenuItem
                            x:Name="ManageServerTargetsMenuItem"
                            Height="35"
                            Command="{Binding ServerSync.ManageServerTargetsCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                            Header="Server Targets..."
                            Visibility="{Binding ServerSync.IsServerOptionsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource IMM.BooleanToVisibilityConverter}}" />
                        <MenuItem
                            x:Name="SyncToServerMenuItem"
                            Height="35"
                            Command="{Binding ServerSync.SyncToServerCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                            Header="Sync to Server..."
                            Visibility="{Binding ServerSync.IsServerOptionsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource IMM.BooleanToVisibilityConverter}}" />
                        <Separator
                            x:Name="ServerOptionsSeparator2"
                            Visibility="{Binding ServerSync.IsServerOptionsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource IMM.BooleanToVisibilityConverter}}" />
```

Note: `SyncToServerMenuItem`'s static `IsEnabled="False"` is intentionally removed — `Command="{Binding ServerSync.SyncToServerCommand, ...}"` now drives `IsEnabled` from `SyncToServerCommand`'s `CanExecute` (backed by `ServerSyncViewModel.CanSyncToServer()`), which starts `false` for a non-server profile exactly as the old default did, but now also correctly re-enables/disables as the active profile changes without needing an explicit `UpdateSyncToServerMenuState()` push at every call site.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build ./ImprovedModMenu.sln --configuration Release`
Expected: 0 errors, 0 warnings. This also resolves Task 3 Step 4's expected "handler not found" state — confirm those errors are gone.

- [ ] **Step 4: Run `verify-methods.sh` to confirm the deleted methods are actually gone**

Run: `./verify-methods.sh EnableServerOptionsMenuItem_OnClick ManageServerTargetsMenuItem_OnClick SyncToServerMenuItem_OnClick UpdateServerOptionsState UpdateSyncToServerMenuState`
Expected: every name reports `[OK] Name: 0` (all five should no longer exist as methods).

- [ ] **Step 5: Run the full test suite one more time**

Run: `dotnet test ./ImprovedModMenu.sln --configuration Release`
Expected: all tests pass, same count as Task 2 Step 6.

- [ ] **Step 6: Commit**

```bash
git add VintageStoryModManager/Views/MainWindow.xaml
git commit -m "refactor: bind server-sync menu items to ServerSyncViewModel commands"
```

---

### Task 5: Smoke test and phase close-out

**Files:**
- Create: `docs\superpowers\specs\2026-07-11-mvvm-command-pattern-pilot-smoke-checklist.md`

Per CLAUDE.md's rule 3 ("Verify with smoke tests... needs a checklist markdown file listing each item to test"). This is a manual step — the user runs the app and works through the checklist; there is no automated substitute in this environment (no WPF GUI harness).

- [ ] **Step 1: Write the smoke-test checklist**

Create `docs\superpowers\specs\2026-07-11-mvvm-command-pattern-pilot-smoke-checklist.md`:

```markdown
# MVVM Command Pattern Pilot — Smoke Test Checklist

Covers the Area H (Server Sync) command conversion. Run against a Server-type profile
and a Local-type profile.

- [ ] On a Local profile: "Sync to Server..." is disabled (grayed out) in the File menu.
- [ ] Toggle "Enable server options" on: "Server Targets...", "Sync to Server...", and both
      separators around them become visible in the File menu.
- [ ] Toggle "Enable server options" off: those same items/separators disappear again.
- [ ] Toggle "Enable server options" on, restart the app: the setting persisted (still on).
- [ ] Click "Server Targets...": the Manage Server Targets dialog opens, owned by the main
      window (Alt-Tab shows it grouped with the main window; it stays on top of it).
- [ ] Add or edit a server target, close the dialog: "Sync to Server..." enablement updates
      immediately to reflect whether the active profile now has a usable target.
- [ ] Switch the active profile to Server type with a configured target: "Sync to Server..."
      becomes enabled without needing to reopen any menu.
- [ ] Switch to a Server-type profile with NO server target configured, click
      "Sync to Server...": expect it to be disabled (can't click it) — this is the
      CanSyncToServer() gate.
- [ ] On a Server-type profile with a valid target but no data directory configured
      (if reachable), click "Sync to Server...": expect the themed warning dialog
      "No data directory is configured for this profile."
- [ ] On a Server-type profile with a valid target and data directory, click
      "Sync to Server...": the Sync to Server dialog opens, owned by the main window,
      and functions as before (preview/execute steps unchanged).
- [ ] Copy for Server button (unchanged by this pilot): still works exactly as before —
      confirms the deferred 4th handler wasn't broken by this pass.
```

- [ ] **Step 2: Commit the checklist**

```bash
git add docs/superpowers/specs/2026-07-11-mvvm-command-pattern-pilot-smoke-checklist.md
git commit -m "docs: smoke-test checklist for the MVVM command pattern pilot"
```

- [ ] **Step 3: Hand off to the user for manual smoke testing**

Report to the user: build and tests are green; the checklist above needs a human pass (per CLAUDE.md, no WPF GUI automation in this environment). Do not mark this task complete, tag a milestone, or update `docs/superpowers/specs/mvvm-roadmap.md`/CLAUDE.md's "Up Next" section until the user confirms the checklist passed.
