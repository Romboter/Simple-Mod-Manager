# Slice 18 (Area K bite 1): SettingsMenuViewModel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** First area-K MVVM bite: replace the settings/logging/mod-version toggle handlers in `MainWindow.Settings.cs` (152 lines), `MainWindow.Logging.cs` (84), and `MainWindow.ModVersionSettings.cs` (25) with a bound `SettingsMenuViewModel` over `UserConfigurationService`, dialogs routed through `IConfirmationService`.

**Architecture:** Unlike area-M slices, **this slice edits `MainWindow.xaml` and deletes code-behind handlers — that is the point.** Plain toggles become two-way `IsChecked` bindings; the two *gated* toggles (auto-refresh warning, automatic-backups validation+warning) become `IsChecked="{Binding X, Mode=OneWay}"` + `AsyncRelayCommand`, because their confirmation flow is async and a declined prompt must leave the property untouched (natural revert). The VM is view-owned: `MainWindow` constructs it (it owns `_userConfiguration`, `_dataDirectory`, the trace listener) and exposes it as a `SettingsMenu` property bound via `RelativeSource AncestorType=Window` — `MainViewModel` is not touched (area-M continuous rule: nothing may grow it).

**Tech Stack:** .NET 8 WPF, CommunityToolkit `ObservableObject`/`AsyncRelayCommand`, xUnit.

## Global Constraints

- **Base commit: `e504f0d`.** This slice's files (`MainWindow.Settings.cs`, `MainWindow.Logging.cs`, `MainWindow.ModVersionSettings.cs`, `MainWindow.xaml`, new files) are **disjoint from slices 15–17** (all `MainViewModel.cs`/`Services/`), so it can execute in parallel with them or in any order. Still: re-run the greps at your actual base; stop-and-report on contradictions.
- **Behavior parity, not surface parity:** the golden rule here is that every toggle behaves exactly as before (same config writes, same warnings shown once, same declines reverting, same side effects). Menu item `x:Name`s stay (other code references them only where noted — verify with the Step-1 grep).
- **Move, don't rewrite** the gate logic — conditions and message strings verbatim into the VM; enumerate every non-verbatim change under "Adaptations".
- Release build zero warnings/errors; scoped IDE0005 gate.
- **Worktree environment check (first action):** standard — toplevel under `.claude/worktrees/`, `git reset --hard <TIP-COMMIT>` in own worktree only, stop-and-report on anomalies. Standing permission to abandon.

---

### Task 1: Seams — `IUserConfigurationService` (narrow) + `NotifyAsync` on `IConfirmationService`

**Files:**
- Create: `VintageStoryModManager/Services/IUserConfigurationService.cs`
- Modify: `VintageStoryModManager/Services/UserConfigurationService.cs` (add `: IUserConfigurationService` — members already exist)
- Modify: `VintageStoryModManager/Services/IConfirmationService.cs`, `VintageStoryModManager/Services/ConfirmationService.cs`

**Why:** `UserConfigurationService` is `sealed` with a parameterless ctor that touches the real user config — tests need a fake. The interface contains **exactly** the getter/setter pairs the new VM uses, nothing more (grep each name in `UserConfigurationService.cs` first and copy the real signatures — property vs method shapes must match what exists; expected set):

```csharp
public interface IUserConfigurationService
{
    bool DisableAutoRefreshWarningAcknowledged { get; }
    void SetDisableAutoRefreshWarningAcknowledged(bool value);
    void SetDisableAutoRefresh(bool value);
    bool AutomaticDataBackupsWarningAcknowledged { get; }
    void SetAutomaticDataBackupsWarningAcknowledged(bool value);
    bool AutomaticDataBackupsEnabled { get; }
    void SetAutomaticDataBackupsEnabled(bool value);
    void SetDisableInternetAccess(bool value);
    void SetModlistAutoLoadBehavior(ModlistAutoLoadBehavior value);
    bool UseFasterThumbnails { get; }
    void SetUseFasterThumbnails(bool value);
    bool CacheAllVersionsLocally { get; }
    void SetCacheAllVersionsLocally(bool value);
    bool RequireExactVsVersionMatch { get; }
    void SetRequireExactVsVersionMatch(bool value);
    bool LogModUpdates { get; }
    void SetLogModUpdates(bool value);
    bool LogModInstalls { get; }
    void SetLogModInstalls(bool value);
    bool LogModDeletions { get; }
    void SetLogModDeletions(bool value);
    bool LogAppLaunchAndExit { get; }
    void SetLogAppLaunchAndExit(bool value);
    bool LogErrorsAndExceptions { get; }
    void SetLogErrorsAndExceptions(bool value);
    // + the getters needed for initial checked state of DisableAutoRefresh / internet access /
    //   modlist auto-load behavior — find their exact names in UserConfigurationService.cs
    //   (grep 'DisableAutoRefresh\|DisableInternetAccess\|ModlistAutoLoad') and include them.
}
```

`IConfirmationService` gains one member, `ConfirmationService` implements it with the OK-only message box (mirror however `ConfirmAsync` shows its box):

```csharp
Task NotifyAsync(string message, string title);
```

- [ ] **Step 1:** Grep check — every interface member must already exist on `UserConfigurationService` with the same shape; also `grep -rn 'IConfirmationService' --include='*.cs' VintageStoryModManager/ | grep -v obj/` to find all implementors (each needs `NotifyAsync`; expected: `ConfirmationService` only — stop and report if there are more and adding the member is non-trivial).
- [ ] **Step 2:** Build Release → 0/0. Commit: `refactor: add IUserConfigurationService and NotifyAsync seams (slice 18 prep, area K)`

---

### Task 2: `SettingsMenuViewModel` + XAML bindings + delete handlers

**Files:**
- Create: `VintageStoryModManager/ViewModels/SettingsMenuViewModel.cs`
- Modify: `VintageStoryModManager/Views/MainWindow.xaml`
- Modify: `VintageStoryModManager/Views/MainWindow/MainWindow.Settings.cs`, `MainWindow.Logging.cs` (delete `MainWindow.ModVersionSettings.cs` entirely)
- Modify: whichever partial constructs things post-`_userConfiguration` (find the right ctor/init spot in `MainWindow.*.cs` — likely `Core.cs` ctor region; a one-line property init is the only change there)

**Interfaces — Produces (full class; gate bodies verbatim from the handlers):**

```csharp
namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Bound view-model for the settings/logging toggle menu items. Owns the toggle state and the
///     acknowledge-once warning gates; dialogs go through IConfirmationService, side effects that
///     belong to the window (trace listener, view-model notifications) through the callbacks.
/// </summary>
public sealed class SettingsMenuViewModel : ObservableObject
{
    private readonly IUserConfigurationService _configuration;
    private readonly IConfirmationService _confirmation;
    private readonly Func<string?> _dataDirectoryProvider;      // () => _dataDirectory
    private readonly Action<bool> _onAutoRefreshDisabledChanged; // d => _viewModel?.SetAutoRefreshDisabled(d)
    private readonly Action _onInternetAccessStateChanged;       // () => _viewModel?.OnInternetAccessStateChanged()
    private readonly Action _onErrorLoggingChanged;              // () => InitializeTraceListener()

    public SettingsMenuViewModel(
        IUserConfigurationService configuration,
        IConfirmationService confirmation,
        Func<string?> dataDirectoryProvider,
        Action<bool> onAutoRefreshDisabledChanged,
        Action onInternetAccessStateChanged,
        Action onErrorLoggingChanged)
    { /* ThrowIfNull each; assign; initialize backing fields from configuration getters;
         ToggleDisableAutoRefreshCommand = new AsyncRelayCommand(ToggleDisableAutoRefreshAsync);
         ToggleAutomaticDataBackupsCommand = new AsyncRelayCommand(ToggleAutomaticDataBackupsAsync); */ }

    // --- gated toggles: OneWay-bound property + command ---
    public bool DisableAutoRefresh { get; private set; }             // SetProperty in the command
    public IAsyncRelayCommand ToggleDisableAutoRefreshCommand { get; }

    public bool AutomaticDataBackupsEnabled { get; private set; }
    public IAsyncRelayCommand ToggleAutomaticDataBackupsCommand { get; }

    // --- plain two-way toggles (each setter: SetProperty + config write + side effect if listed) ---
    public bool DisableInternetAccess { get; set; }   // setter also: InternetAccessManager.SetInternetAccessDisabled(value); _onInternetAccessStateChanged();
    public bool UseFasterThumbnails { get; set; }
    public bool CacheAllVersionsLocally { get; set; }
    public bool RequireExactVsVersionMatch { get; set; }
    public bool LogModUpdates { get; set; }
    public bool LogModInstalls { get; set; }
    public bool LogModDeletions { get; set; }
    public bool LogAppLaunchAndExit { get; set; }
    public bool LogErrorsAndExceptions { get; set; }  // setter also: _onErrorLoggingChanged();

    // --- modlist auto-load pair (mutually exclusive; unchecking either → Prompt) ---
    public bool AlwaysClearModlists { get; set; }  // true → SetModlistAutoLoadBehavior(Replace); false → Prompt; setting true flips AlwaysAddModlists off
    public bool AlwaysAddModlists { get; set; }    // true → Add; false → Prompt; symmetric
}
```

Gated command bodies — logic verbatim from `MainWindow.Settings.cs` handlers, translated to the async seam:

```csharp
private async Task ToggleDisableAutoRefreshAsync()
{
    var disable = !DisableAutoRefresh;   // command toggles the current state

    if (disable && !_configuration.DisableAutoRefreshWarningAcknowledged)
    {
        var message = /* the exact multi-line string from MainWindow.Settings.cs */;
        if (!await _confirmation.ConfirmAsync(message, "Simple VS Manager"))
            return;   // declined: property untouched, menu stays unchecked via OneWay binding

        _configuration.SetDisableAutoRefreshWarningAcknowledged(true);
    }

    _configuration.SetDisableAutoRefresh(disable);
    SetProperty(ref /* backing field */, disable, nameof(DisableAutoRefresh));
    _onAutoRefreshDisabledChanged(disable);
}

private async Task ToggleAutomaticDataBackupsAsync()
{
    var enable = !AutomaticDataBackupsEnabled;

    if (enable)
    {
        var dataDirectory = _dataDirectoryProvider();
        if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory))
        {
            await _confirmation.NotifyAsync(
                "Please configure a valid VintagestoryData folder before enabling automatic backups.",
                "Simple VS Manager");
            return;
        }

        if (!_configuration.AutomaticDataBackupsWarningAcknowledged)
        {
            const string message = /* exact string from the handler */;
            await _confirmation.NotifyAsync(message, "Simple VS Manager");
            _configuration.SetAutomaticDataBackupsWarningAcknowledged(true);
        }
    }

    _configuration.SetAutomaticDataBackupsEnabled(enable);
    SetProperty(ref /* backing */, _configuration.AutomaticDataBackupsEnabled, nameof(AutomaticDataBackupsEnabled));
}
```

(Note the last line re-reads the config, mirroring the original's `menuItem.IsChecked = _userConfiguration.AutomaticDataBackupsEnabled;` re-sync. Preserve any similar re-read in the plain toggles too — check each original handler; where the original re-read after set, the setter should too.)

**XAML rewires (find each menu item by its current `Click=` handler name in `MainWindow.xaml`):**
- Plain toggles: remove `Click="..."`, ensure `IsCheckable="True"`, add `IsChecked="{Binding SettingsMenu.<Prop>, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}"`.
- Gated pair: `IsChecked="{Binding SettingsMenu.<Prop>, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}"` + `Command="{Binding SettingsMenu.Toggle<X>Command, RelativeSource={RelativeSource AncestorType=Window}}"`.
- Modlist pair: two-way like plain toggles (mutual exclusion lives in the VM setters).

**Code-behind:**
- `MainWindow` gets `public SettingsMenuViewModel SettingsMenu { get; }` initialized in the ctor **after** `_userConfiguration` exists and **before** `InitializeComponent()` if ordering allows (if `_userConfiguration` is created after `InitializeComponent`, initialize `SettingsMenu` there and the bindings will still resolve on first layout — verify which by reading `Core.cs`'s ctor; report which ordering you found):

```csharp
SettingsMenu = new SettingsMenuViewModel(
    _userConfiguration,
    new ConfirmationService(/* mirror however other MainWindow code constructs it — grep first */),
    () => _dataDirectory,
    disable => _viewModel?.SetAutoRefreshDisabled(disable),
    () => _viewModel?.OnInternetAccessStateChanged(),
    InitializeTraceListener);
```

- Delete handlers: all of `MainWindow.ModVersionSettings.cs` (file removal); from `Settings.cs`: the five toggle/auto-load handlers + `HandleModlistAutoLoadMenuClick` + `SetModlistAutoLoadBehavior` + `UpdateModlistAutoLoadMenu`; from `Logging.cs`: the five log toggles + `UpdateLoggingMenuState`. **Keep**: `UpdateGameVersionMenuItem` (display glue, stays in `Settings.cs`), `InitializeTraceListener` (stays in `Logging.cs`, now also a VM callback).
- **Caller cleanup (grep, don't assume):** `grep -rn 'UpdateLoggingMenuState\|UpdateModlistAutoLoadMenu\|SetModlistAutoLoadBehavior' VintageStoryModManager/ --include='*.cs' | grep -v obj/` — every external call site (likely startup/init sync code) must be deleted or rewired; if one does something the bindings don't cover (e.g., setting behavior programmatically from modlist-load code), **stop and report** with the call site — that's a scope boundary, not something to improvise through.

**Specified adaptations:** async command translation for the two gated toggles (Yes/No box → `ConfirmAsync`, OK boxes → `NotifyAsync`); `menuItem.IsChecked` reverts → OneWay-binding no-op; menu-sync methods → bindings; everything else verbatim.

- [ ] **Step 1:** Grep inventory — every `Click=` handler named in the three partials found in `MainWindow.xaml`; the caller-cleanup grep above; `grep -rn 'AlwaysClearModlistsMenuItem\|AlwaysAddModlistsMenuItem\|LogModUpdateMenuItem\|GameVersionMenuItem' --include='*.cs' VintageStoryModManager/Views/ | grep -v obj/` to find code-behind references to the named items that the deletions must not orphan.
- [ ] **Step 2:** Implement VM, XAML, deletions, wiring.
- [ ] **Step 3:** Gates: Release build 0/0; stale grep — `grep -n 'OnClick' VintageStoryModManager/Views/MainWindow/MainWindow.Settings.cs VintageStoryModManager/Views/MainWindow/MainWindow.Logging.cs` → zero hits; `MainWindow.ModVersionSettings.cs` gone; IDE0005 scoped clean.
- [ ] **Step 4:** Commit: `refactor: bind settings/logging toggles to SettingsMenuViewModel (slice 18, area K)`

---

### Task 3: Tests

**Files:**
- Create: `VintageStoryModManager.Tests/SettingsMenuViewModelTests.cs`

Fakes: `FakeUserConfiguration : IUserConfigurationService` (auto-property bag), `FakeConfirmation : IConfirmationService` (scripted `bool` answer; records messages/titles for both methods).

Test list (~9):
1. `DisableAutoRefresh_FirstEnable_Declined_NothingChanges` — not acknowledged, confirm answers false: property false, `SetDisableAutoRefresh` never called, acknowledged still false.
2. `DisableAutoRefresh_FirstEnable_Accepted` — confirm true: acknowledged set, config set, property true, callback saw `true`.
3. `DisableAutoRefresh_AlreadyAcknowledged_NoPrompt` — confirm service records zero calls; config set directly.
4. `DisableAutoRefresh_Disable_NoPrompt` — start true, toggle: no prompt, config false, callback `false`.
5. `Backups_InvalidDataDir_RevertsWithNotify` — provider returns null: `NotifyAsync` called, config never written, property false.
6. `Backups_FirstEnable_NotifiesOnce_SetsAcknowledged` — valid temp dir: notify called once, acknowledged set, enabled set; second enable cycle → no notify.
7. `ModlistPair_MutuallyExclusive` — set `AlwaysClearModlists = true`: config got `Replace`, `AlwaysAddModlists` flipped false; then `AlwaysAddModlists = true`: config `Add`, clear flipped false; then `AlwaysAddModlists = false`: config `Prompt`.
8. `PlainToggle_WritesConfig` — `LogModUpdates = true` → config setter called; `LogErrorsAndExceptions = true` → error-logging callback fired.
9. `InternetToggle_FiresStateCallback` — `DisableInternetAccess = true` → state-changed callback fired, config written. (`InternetAccessManager` static: read its source first — if `SetInternetAccessDisabled(true)` in a test process has side effects that could leak between tests, reset it in test cleanup to its prior value.)

- [ ] **Step 1:** Write; run filter → all pass; full Release suite → all pass (known `*_wpftmp` flake handling as usual).
- [ ] **Step 2:** Commit: `test: cover SettingsMenuViewModel (slice 18, area K)`

---

### Task 4: Final gates + report

- [ ] Release build 0/0; scoped IDE0005 clean.
- [ ] `git diff --name-only` off base: the VM, the two seam files + `UserConfigurationService.cs` + `ConfirmationService.cs`, `MainWindow.xaml`, `Settings.cs`, `Logging.cs`, deleted `ModVersionSettings.cs`, test file, and (if needed) the one-line ctor wiring file — nothing else.
- [ ] Report: worktree/branch, commit hashes, Adaptations, the ctor-ordering finding (SettingsMenu init relative to `InitializeComponent`), any stop-and-report call sites found, test results. No push; no `CLAUDE.md`/`.claude/`.

---

## Smoke test (orchestrator/user, post-merge) — heavier than area-M slices because XAML changed

Open the menus and verify every toggle's checkmark matches persisted config on startup. Then: toggle Disable Auto Refresh with a **fresh config** (or acknowledged flag cleared) → warning appears, decline → checkmark stays off; accept → sticks and auto-refresh actually stops; toggle Automatic Data Backups with no valid data dir → warning + stays off; with valid dir → info shown once, sticks across restart; flip modlist auto-load between Clear/Add/neither → mutual exclusion in the menu and behavior on next modlist load; toggle Disable Internet Access → tabs disable (slice-14 behavior) and mod rows grey; flip each log toggle and confirm log output appears/stops (error logging: check the trace listener reinitializes — trigger any handled error); restart and re-verify all checkmarks.
