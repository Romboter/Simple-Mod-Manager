# Slice 14 (Area M): Tab-Navigation Facade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the View Section / Tab Navigation cluster (~140 lines: `ViewSection` enum, `_viewSection`, three tab commands, tab booleans, `CurrentModsView` mapping, `SetViewSection` gates) out of `MainViewModel` into a new `TabNavigationViewModel`, with `MainViewModel` keeping thin pass-throughs so every XAML binding path and `MainWindow.*.cs` call site is untouched.

**Architecture:** `TabNavigationViewModel` owns the section state machine, the internet-access gates, and the three commands. The UI side effects of a tab switch (clearing search/selection, per-tab status message, `PropertyChanged` raises, deferred `FastCheck`) stay in `MainViewModel` as a single callback — bindings listen to `MainViewModel`'s `PropertyChanged`, so the raises must keep coming from it.

**Tech Stack:** .NET 8 WPF, CommunityToolkit-style `RelayCommand` (match `MainViewModel`'s existing using for it), xUnit.

## Global Constraints

- **Base commit: `2643d75`** (post-slice-13 tip). All of this plan's claims were re-verified against that commit on 2026-07-07 (full grep pass: all 28 in-file references to `_viewSection`/`ViewSection.`/the const accounted for; zero `ViewSection` users outside `MainViewModel.cs`; Views/ surface baseline = 25 hits for Task 1 Step 4). Quoted line numbers are approximate — `SetViewSection` is at 573–621, the enum at 3590–3595, the const at line 25, `RefreshInternetAccessDependentState` at 3418–3449, command fields at 68–70, `_viewSection` field at 132, command construction at 205–214. Re-run any grep whose result you depend on; if one contradicts the plan, stop and report before editing.
- **Golden rule: `MainViewModel`'s public surface does not change.** XAML binds `IsViewingMainTab`, `IsViewingModlistTab`, `SearchModDatabase`, `CurrentModsView` (e.g. `MainWindow.xaml:364,2150,2156,2414`, `MainWindowStyles.xaml:255,268`, `NavigationHeader.xaml:83`); `MainWindow.*.cs` uses those plus `ShowMainTabCommand`/`ShowDatabaseTabCommand`/`ShowModlistTabCommand` (`MainWindow.Navigation.cs`, `MainWindow.ModlistLoading.cs:124`, `MainWindow.CloudLoad.cs:234`, `MainWindow.CloudManagement.cs:242`, `MainWindow.CloudSave.cs:135,144`, `MainWindow.ModGridSelection.cs:28`, `MainWindow.ViewModel.cs:116,132,165`, `MainWindow.ViewModelPropertyChanges.cs:32,44,52`). All of these stay as `MainViewModel` members with identical names/types. `MainWindow.ViewModelPropertyChanges.cs` compares `e.PropertyName` against `nameof(MainViewModel.IsViewingModlistTab)` etc. — the `PropertyChanged` raises must therefore keep coming from `MainViewModel` itself.
- **Move, don't rewrite.** Bodies verbatim except the adaptations specified below; enumerate every non-verbatim change under an "Adaptations" heading in your report.
- Release build zero warnings/errors; IDE0005 format gate per changed file; `./verify-methods.sh` for moved methods; this slice touches **no** `Views/` files.
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` (orchestrator-supplied) in your own worktree only; stop and report on any anomaly. Standing permission to abandon and report rather than force a tangled extraction.

---

### Task 1: Extract `TabNavigationViewModel` and rewire `MainViewModel`

**Files:**
- Create: `VintageStoryModManager/ViewModels/TabNavigationViewModel.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.ViewModels;

/// <summary>Which of the three main tabs is active.</summary>
public enum ViewSection   // moved from MainViewModel's private nested enum — same member names/order
{
    MainTab,
    DatabaseTab,
    ModlistTab
}

/// <summary>
///     Owns the active-tab state machine: which section is current, the internet-access gates on the
///     Database/Modlist tabs, and the three tab-switch commands. UI side effects of a switch (search/
///     selection clearing, status text, property-change notification, fast-check) stay with the owner
///     via the onSectionChanged callback, because XAML binds the owner's properties.
/// </summary>
public sealed class TabNavigationViewModel
{
    internal const string InternetAccessDisabledStatusMessage =
        "Enable Internet Access in the File menu to use.";   // moved from MainViewModel.cs:27

    private readonly Func<bool> _isInternetAccessDisabled;
    private readonly Action<string> _setStatus;              // gate-refusal status text
    private readonly Action<ViewSection> _onSectionChanged;  // fires AFTER Current changes, successful switches only
    private readonly ICollectionView _modsView;
    private readonly ICollectionView _searchResultsView;
    private readonly ICollectionView _cloudModlistsView;
    private readonly RelayCommand _showMainTabCommand;
    private readonly RelayCommand _showDatabaseTabCommand;
    private readonly RelayCommand _showModlistTabCommand;

    public TabNavigationViewModel(
        ICollectionView modsView,
        ICollectionView searchResultsView,
        ICollectionView cloudModlistsView,
        Func<bool> isInternetAccessDisabled,
        Action<string> setStatus,
        Action<ViewSection> onSectionChanged)
    {
        ArgumentNullException.ThrowIfNull(modsView);
        ArgumentNullException.ThrowIfNull(searchResultsView);
        ArgumentNullException.ThrowIfNull(cloudModlistsView);
        ArgumentNullException.ThrowIfNull(isInternetAccessDisabled);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(onSectionChanged);

        _modsView = modsView;
        _searchResultsView = searchResultsView;
        _cloudModlistsView = cloudModlistsView;
        _isInternetAccessDisabled = isInternetAccessDisabled;
        _setStatus = setStatus;
        _onSectionChanged = onSectionChanged;

        _showMainTabCommand = new RelayCommand(() => SetViewSection(ViewSection.MainTab));
        _showDatabaseTabCommand = new RelayCommand(
            () => SetViewSection(ViewSection.DatabaseTab),
            () => !_isInternetAccessDisabled());
        _showModlistTabCommand = new RelayCommand(
            () => SetViewSection(ViewSection.ModlistTab),
            () => !_isInternetAccessDisabled());
    }

    public ViewSection Current { get; private set; } = ViewSection.MainTab;

    public IRelayCommand ShowMainTabCommand => _showMainTabCommand;
    public IRelayCommand ShowDatabaseTabCommand => _showDatabaseTabCommand;
    public IRelayCommand ShowModlistTabCommand => _showModlistTabCommand;

    public bool IsViewingMainTab => Current == ViewSection.MainTab;
    public bool IsViewingModlistTab => Current == ViewSection.ModlistTab;
    public bool SearchModDatabase => Current == ViewSection.DatabaseTab;

    public ICollectionView CurrentModsView => Current switch
    {
        ViewSection.DatabaseTab => _searchResultsView,
        ViewSection.ModlistTab => _cloudModlistsView,
        _ => _modsView
    };

    public void SetViewSection(ViewSection section)
    {
        if (Current == section) return;

        if (section == ViewSection.DatabaseTab && _isInternetAccessDisabled())
        {
            _setStatus(InternetAccessDisabledStatusMessage);
            return;
        }

        if (section == ViewSection.ModlistTab && _isInternetAccessDisabled())
        {
            _setStatus(InternetAccessDisabledStatusMessage);
            return;
        }

        Current = section;

        _onSectionChanged(section);
    }

    public void NotifyInternetAccessChanged()
    {
        _showDatabaseTabCommand.NotifyCanExecuteChanged();
        _showModlistTabCommand.NotifyCanExecuteChanged();
    }
}
```

(Add the `using` directives the file actually needs — `System.ComponentModel` for `ICollectionView`, plus the CommunityToolkit namespace for `RelayCommand`/`IRelayCommand` — `MainViewModel`'s existing declarations are `public IRelayCommand ShowMainTabCommand { get; }` (lines ~395–399, verified at `2643d75`), so the facade exposes `IRelayCommand`, not `ICommand`, to keep those assignments compiling. Run the IDE0005 gate after.)

**`MainViewModel` changes:**

1. **Delete:** `private ViewSection _viewSection = ViewSection.MainTab;` (line ~152), the three `private readonly RelayCommand _show*TabCommand` fields (~75–77), `private void SetViewSection(...)` (~573–621), the private nested `enum ViewSection` (~4345), and the `InternetAccessDisabledStatusMessage` const (line 27). The moved enum lives in the new file with **identical member names** — all `ViewSection.X` references that remain in `MainViewModel` compile against it unchanged.

2. **Add field:** `private readonly TabNavigationViewModel _tabNavigation;`

3. **Ctor** — replace the command construction block (~200–209) with:

```csharp
_tabNavigation = new TabNavigationViewModel(
    ModsView,
    SearchResultsView,
    CloudModlistsView,
    () => InternetAccessManager.IsInternetAccessDisabled,
    message => SetStatus(message, false),
    OnTabSectionChanged);
ShowMainTabCommand = _tabNavigation.ShowMainTabCommand;
ShowDatabaseTabCommand = _tabNavigation.ShowDatabaseTabCommand;
ShowModlistTabCommand = _tabNavigation.ShowModlistTabCommand;
```

(This must sit **after** `ModsView`/`SearchResultsView`/`CloudModlistsView` are assigned — they already are by line 200. The `ShowXTabCommand { get; }` auto-properties keep their current declarations, unchanged surface.)

4. **Pass-throughs** replacing the old bodies (same names, same accessibility):

```csharp
public ICollectionView CurrentModsView => _tabNavigation.CurrentModsView;
public bool IsViewingModlistTab => _tabNavigation.IsViewingModlistTab;
public bool IsViewingMainTab => _tabNavigation.IsViewingMainTab;
public bool SearchModDatabase => _tabNavigation.SearchModDatabase;
```

5. **New private callback** — the verbatim tail of the old `SetViewSection` (everything after `_viewSection = section;`):

```csharp
private void OnTabSectionChanged(ViewSection section)
{
    if (!string.IsNullOrEmpty(_searchText)) SearchText = string.Empty;

    switch (section)
    {
        case ViewSection.DatabaseTab:
            SelectedMod = null;
            SetStatus("Showing mod database.", false);
            break;
        case ViewSection.MainTab:
            SelectedMod = null;
            SetStatus("Showing installed mods.", false);
            break;
        case ViewSection.ModlistTab:
            SelectedMod = null;
            SetStatus("Showing cloud modlists.", false);
            break;
    }

    // Notify critical property changes immediately
    OnPropertyChanged(nameof(IsViewingModlistTab));
    OnPropertyChanged(nameof(IsViewingMainTab));
    OnPropertyChanged(nameof(SearchModDatabase));
    OnPropertyChanged(nameof(CurrentModsView));

    // Defer non-critical property changes to avoid blocking UI thread during tab switch
    Application.Current?.Dispatcher.BeginInvoke(() =>
    {
        if (_tabNavigation.Current == ViewSection.MainTab) FastCheck();
    }, DispatcherPriority.Background);
}
```

6. **`RefreshInternetAccessDependentState`** (~4173–4204): replace the two `NotifyCanExecuteChanged()` calls with `_tabNavigation.NotifyInternetAccessChanged();`, replace `_viewSection == ViewSection.DatabaseTab`/`ModlistTab` reads with `_tabNavigation.Current == ...`, replace both `SetViewSection(ViewSection.MainTab)` calls with `_tabNavigation.SetViewSection(ViewSection.MainTab)`, and both `InternetAccessDisabledStatusMessage` references with `TabNavigationViewModel.InternetAccessDisabledStatusMessage`. (Grep for any other const/enum references first: `grep -n 'InternetAccessDisabledStatusMessage\|_viewSection\|ViewSection\.' VintageStoryModManager/ViewModels/MainViewModel.cs` — every remaining hit must be rewired or be inside code this plan already accounts for.)

**Specified adaptations (report them all):**
1. `InternetAccessManager.IsInternetAccessDisabled` static reads in the gates/CanExecute → injected `Func<bool>` (testability).
2. `SetStatus(msg, false)` in the gates → `Action<string>` delegate.
3. Post-switch UI side effects → `onSectionChanged` callback (`OnTabSectionChanged`, body verbatim from the old method tail; the deferred-dispatch check reads `_tabNavigation.Current` instead of `_viewSection`).
4. `ViewSection` enum: private nested → public top-level in the new file, same members.
5. `InternetAccessDisabledStatusMessage` const moved to `TabNavigationViewModel` (internal).
6. Command fields become the facade's; `MainViewModel`'s `ShowXTabCommand` properties now assigned from it.

- [ ] **Step 1:** Create `TabNavigationViewModel.cs` as specified.
- [ ] **Step 2:** Rewire `MainViewModel` (deletions, field, ctor, pass-throughs, callback, `RefreshInternetAccessDependentState`). Stale-reference gate:

```bash
grep -n '_viewSection\|_showMainTabCommand\|_showDatabaseTabCommand\|_showModlistTabCommand\|private enum ViewSection\|private const string InternetAccessDisabledStatusMessage' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: **zero hits.**

- [ ] **Step 3: Build + verify**

```bash
dotnet build ./ImprovedModMenu.sln --configuration Release
./verify-methods.sh SetViewSection NotifyInternetAccessChanged OnTabSectionChanged
```

Expected: build clean; `SetViewSection: 1` (now only in the facade), `NotifyInternetAccessChanged: 1`, `OnTabSectionChanged: 1`.

- [ ] **Step 4: Golden-rule grep** — `git diff --name-only` contains no `Views/` or `.xaml` files, and:

```bash
grep -rn 'IsViewingMainTab\|IsViewingModlistTab\|SearchModDatabase\|CurrentModsView\|ShowMainTabCommand\|ShowDatabaseTabCommand\|ShowModlistTabCommand' --include='*.cs' VintageStoryModManager/Views/ | wc -l
```

must match the pre-change count (run it before editing and record the number).

- [ ] **Step 5: Format + commit**

```bash
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/ViewModels/TabNavigationViewModel.cs VintageStoryModManager/ViewModels/MainViewModel.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
git add VintageStoryModManager/ViewModels/TabNavigationViewModel.cs VintageStoryModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: extract tab-navigation facade from MainViewModel (slice 14, area M)"
```

---

### Task 2: Tests

**Files:**
- Create: `VintageStoryModManager.Tests/TabNavigationViewModelTests.cs`

**Interfaces — Consumes:** Task 1's `TabNavigationViewModel` public API. Follow the existing style in `ModlistCollectionsViewModelTests.cs` — including however it constructs `ICollectionView` instances (WPF's `ListCollectionView` over a `List<object>` works without a dispatcher; mirror the existing precedent).

Test list (~7; construct with recording stub delegates — a `List<string> statusMessages`, `List<ViewSection> sectionChanges`, `bool offline` captured by the `Func<bool>`):

1. `SetViewSection_SameSection_NoCallbacks` — switching to `MainTab` while on `MainTab`: no status, no section-changed, `Current` unchanged.
2. `SetViewSection_DatabaseTab_Offline_Blocked` — `offline = true`: `Current` stays `MainTab`, `statusMessages` contains `TabNavigationViewModel.InternetAccessDisabledStatusMessage`, `sectionChanges` empty.
3. `SetViewSection_ModlistTab_Offline_Blocked` — same shape.
4. `SetViewSection_Online_Switches` — `Current` becomes `DatabaseTab`, `sectionChanges == [DatabaseTab]`, no status message from the gate (the "Showing…" statuses are the owner's job, not the facade's).
5. `CurrentModsView_MapsPerSection` — `MainTab`→modsView instance, `DatabaseTab`→searchResultsView, `ModlistTab`→cloudModlistsView (reference equality; switch while online).
6. `ShowDatabaseTabCommand_CanExecute_TracksInternetAccess` — `offline=true` → `CanExecute` false; flip the captured bool to false → true. Same for Modlist command; `ShowMainTabCommand.CanExecute` always true.
7. `NotifyInternetAccessChanged_RaisesCanExecuteChanged` — subscribe to `ShowDatabaseTabCommand.CanExecuteChanged`, call `NotifyInternetAccessChanged()`, assert it fired.

- [ ] **Step 1:** Write the tests; run `dotnet test VintageStoryModManager.Tests --filter TabNavigationViewModelTests` → all pass. (Known flake: `*_wpftmp` CS0103 from stale Debug obj — re-run once, then `dotnet clean ... --configuration Debug` if persistent.)
- [ ] **Step 2:** Full suite: `dotnet test ./ImprovedModMenu.sln --configuration Release` → all pass.
- [ ] **Step 3: Commit**

```bash
git add VintageStoryModManager.Tests/TabNavigationViewModelTests.cs
git commit -m "test: cover TabNavigationViewModel (slice 14, area M)"
```

---

### Task 3: Final gates + report

- [ ] `dotnet build ./ImprovedModMenu.sln --configuration Release` → zero warnings/errors.
- [ ] `dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes` → clean.
- [ ] Report: worktree path + branch, commit hashes, full Adaptations list, `MainViewModel.cs` line delta, test names/results, any deviations. Do **not** push; do not touch `CLAUDE.md` or stage `.claude/`.

---

## Smoke test (orchestrator/user, post-merge)

Tab-switch round trip Installed→Database→Modlist→Installed (statuses update, grid swaps, search box clears); toggle Internet Access off → Database/Modlist buttons disable and, if on one of those tabs, app bounces to Installed with the "Enable Internet Access…" status; toggle back on → buttons re-enable; land on Installed tab and confirm the deferred FastCheck still fires (update polling tick).
