# Slice 15 (Area M): ModDetailsProgressTracker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the mod-details progress half of the busy-state cluster (~130 lines: pending/total/completed counters, progress computation, the paired mod-details busy scope, and the enqueue/complete transition logic) out of `MainViewModel` into a new `ModDetailsProgressTracker` service, completing what slice 12's `BusyStateTracker` started.

**Architecture:** `ModDetailsProgressTracker` owns the `Interlocked` counter machine and the busy-scope pairing; everything UI-facing stays in `MainViewModel` behind delegates — the four bound properties (`IsLoadingModDetails`, `IsModDetailsProgressVisible`, `ModDetailsProgress`, `ModDetailsStatusText`), dispatcher marshalling, the visibility rule, and all status-bar text decisions (which depend on `MainViewModel`-private flags that `SetStatus` itself maintains). Same pattern as slice 12's `BusyStateTracker`: pure logic in the service, notification glue in the VM.

**Tech Stack:** .NET 8 WPF, xUnit. Style precedent: `VintageStoryModManager/Services/BusyStateTracker.cs` (delegate-injected, synchronously testable).

## Global Constraints

- **Base commit: `e504f0d`** (post-slice-14 tip). Re-verified there on 2026-07-07: all ten cluster members present and structurally identical; body line numbers in the tables below were written at `2643d75` and sit ~26 lines higher now — current anchors: `UpdateModDetailsProgressVisibility` 1905, `UpdateLoadedModsStatus` 1911, `IsModDetailsRefreshPending` 1923, `OnModDetailsRefreshEnqueued` 1928, `OnModDetailsRefreshCompleted` 1950, `EnsureModDetailsBusyScope` 1976, `ReleaseModDetailsBusyScope` 1984, `AddModDetailsWork` 2005, `UpdateModDetailsProgress` 2013, `ResetModDetailsProgress` 2037; enrichment call sites 2374/2681/2706; Dispose scope-release 544; `_isModDetailsStatusActive` write in `SetStatus` 2203. Re-run any grep you depend on; greps beat quoted numbers; stop-and-report on contradictions.
- **Golden rule: `MainViewModel`'s public surface does not change.** XAML binds `IsModDetailsProgressVisible` (`MainWindow.xaml:1977,2027`), `ModDetailsStatusText` (`:2046`), `ModDetailsProgress` (`:2054`), `IsLoadingModDetails` (`MainWindowStyles.xaml:853`); `MainWindow.*.cs` reads `IsLoadingModDetails` (`ModGridRowVisuals.cs:117`, `ModGridViewState.cs:63`, `ModRefresh.cs:122`) and `ViewModelPropertyChanges.cs:74` matches on `nameof(MainViewModel.IsLoadingModDetails)` — so all four properties stay on `MainViewModel` and keep raising its `PropertyChanged`. This slice touches **no** `Views/` or `.xaml` files.
- **Move, don't rewrite.** Bodies verbatim except the delegate substitutions specified below; enumerate every non-verbatim change under an "Adaptations" heading in your report.
- Release build zero warnings/errors; IDE0005 format gate on changed files; `./verify-methods.sh` for moved methods.
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` (orchestrator-supplied) in your own worktree only; verify `VintageStoryModManager/Services/UserReportsCoordinator.cs` exists; stop and report on any anomaly. Standing permission to abandon and report rather than force a tangled extraction.

---

### Task 1: Extract `ModDetailsProgressTracker` and rewire `MainViewModel`

**Files:**
- Create: `VintageStoryModManager/Services/ModDetailsProgressTracker.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Tracks the mod-details refresh workload: a pending-operation count that opens/closes a paired
///     busy scope and a total/completed work counter that drives the details progress bar. Pure
///     counter machine — presentation (dispatcher marshalling, bound properties, status-bar text)
///     stays with the owner via the constructor delegates, mirroring <see cref="BusyStateTracker" />.
/// </summary>
public sealed class ModDetailsProgressTracker
{
    private readonly Func<IDisposable> _beginBusyScope;
    private readonly Func<string> _defaultStageText;            // owner's loading-status message
    private readonly Func<bool> _isModDetailsStatusActive;      // owner's SetStatus-maintained flag
    private readonly Action<bool> _isLoadingChanged;            // owner marshals to dispatcher + sets IsLoadingModDetails
    private readonly Action<double, string> _progressChanged;   // owner sets ModDetailsProgress + ModDetailsStatusText
    private readonly Action _requestLoadingStatus;              // owner: SetStatus(BuildModDetailsLoadingStatusMessage(), false, true)
    private readonly Action _requestReadyStatus;                // owner: SetStatus(BuildModDetailsReadyStatusMessage(), false)

    private readonly object _busyScopeLock = new();
    private IDisposable? _busyScope;
    private int _pendingRefreshCount;
    private int _totalWork;
    private int _completedWork;
    private string _progressStage = string.Empty;

    public ModDetailsProgressTracker(
        Func<IDisposable> beginBusyScope,
        Func<string> defaultStageText,
        Func<bool> isModDetailsStatusActive,
        Action<bool> isLoadingChanged,
        Action<double, string> progressChanged,
        Action requestLoadingStatus,
        Action requestReadyStatus)
    { /* null-check and assign all seven, ArgumentNullException.ThrowIfNull each */ }

    public bool IsRefreshPending =>
        Interlocked.CompareExchange(ref _pendingRefreshCount, 0, 0) > 0;

    public void OnRefreshEnqueued(int count, string? statusText = null);
    public void OnRefreshCompleted(int completedCount = 1);
    public void ReleaseBusyScope();   // for the owner's Dispose path
}
```

**Method bodies — moved verbatim from `MainViewModel` (current locations at `2643d75`) with these renames/substitutions:**

| `MainViewModel` member (line) | Becomes | Substitutions |
|---|---|---|
| `OnModDetailsRefreshEnqueued` (1954–1974) | `OnRefreshEnqueued` | `ResetModDetailsProgress()`→`ResetProgress()`; `EnsureModDetailsBusyScope()`→`EnsureBusyScope()`; `UpdateIsLoadingModDetails(true)`→`_isLoadingChanged(true)`; `AddModDetailsWork(count, statusText)`→`AddWork(count, statusText)`; the final `if (newCount == count \|\| !_isModDetailsStatusActive) SetStatus(BuildModDetailsLoadingStatusMessage(), false, true);`→`if (newCount == count \|\| !_isModDetailsStatusActive()) _requestLoadingStatus();` |
| `OnModDetailsRefreshCompleted` (1976–2000) | `OnRefreshCompleted` | `UpdateModDetailsProgress()`→`UpdateProgress()`; `ReleaseModDetailsBusyScope()`→`ReleaseBusyScope()`; `UpdateIsLoadingModDetails(false)`→`_isLoadingChanged(false)`; `if (_isModDetailsStatusActive) SetStatus(BuildModDetailsReadyStatusMessage(), false);`→`if (_isModDetailsStatusActive()) _requestReadyStatus();`; `ResetModDetailsProgress()`→`ResetProgress()` |
| `EnsureModDetailsBusyScope` (2002–2008) | private `EnsureBusyScope` | `BeginBusyScope()`→`_beginBusyScope()`; `_modDetailsBusyScope`/`_modDetailsBusyScopeLock`→`_busyScope`/`_busyScopeLock` |
| `ReleaseModDetailsBusyScope` (2010–2017) | public `ReleaseBusyScope` | field renames only |
| `AddModDetailsWork` (2031–2037) | private `AddWork` | `UpdateModDetailsProgress(statusText)`→`UpdateProgress(statusText)` |
| `UpdateModDetailsProgress` (2039–2061) | private `UpdateProgress` | `ModDetailsProgress = X; ModDetailsStatusText = Y;` property writes→collect into `_progressChanged(X, Y)` calls (two call points: the zero-total early-out passes `(0, string.Empty)`; the main path passes `(percentage, $"{baseText} ({completed}/{total})")`); `BuildModDetailsLoadingStatusMessage()`→`_defaultStageText()` |
| `ResetModDetailsProgress` (2063–2070) | private `ResetProgress` | trailing property writes→`_progressChanged(0, string.Empty)` |
| `IsModDetailsRefreshPending` (1949–1952) | `IsRefreshPending` property | direct translation |

Counter fields `_pendingModDetailsRefreshCount`, `_modDetailsRefreshTotalWork`, `_modDetailsRefreshCompletedWork`, `_modDetailsProgressStage`, plus `_modDetailsBusyScope`/`_modDetailsBusyScopeLock` (lines 53, 105→n/a, 120, 122, and the two work counters) move as the renamed private fields shown above.

**`MainViewModel` keeps (all unchanged unless noted):**
- Bound properties `IsLoadingModDetails` (291), `IsModDetailsProgressVisible` (304), `ModDetailsProgress` (319), `ModDetailsStatusText` (325) and their backing fields; `UpdateIsLoadingModDetails` (1916, the dispatcher-marshalling setter); `UpdateModDetailsProgressVisibility` (1931, the `&& !_isFastCheckInProgress` rule); `UpdateLoadedModsStatus` (1937 — rewire its `IsModDetailsRefreshPending()` call to `_modDetailsProgressTracker.IsRefreshPending`); `BuildModDetailsLoadingStatusMessage`/`BuildModDetailsReadyStatusMessage` (2019–2029); `SetStatus` and the `_isModDetailsStatusActive`/`_hasShownModDetailsLoadingStatus` flags it maintains (2229–2230); `RecalculateIsBusy`'s `_isLoadingModDetails` read (1901).
- **New field:** `private readonly ModDetailsProgressTracker _modDetailsProgressTracker;`
- **Ctor wiring** (place near the existing `_busyStateTracker` construction):

```csharp
_modDetailsProgressTracker = new ModDetailsProgressTracker(
    BeginBusyScope,
    BuildModDetailsLoadingStatusMessage,
    () => _isModDetailsStatusActive,
    UpdateIsLoadingModDetails,
    (progress, text) =>
    {
        ModDetailsProgress = progress;
        ModDetailsStatusText = text;
    },
    () => SetStatus(BuildModDetailsLoadingStatusMessage(), false, true),
    () => SetStatus(BuildModDetailsReadyStatusMessage(), false));
```

- **Private pass-throughs** so the three enrichment-cluster call sites (2400, 2707, 2732) and any others found by grep stay textually unchanged:

```csharp
private void OnModDetailsRefreshEnqueued(int count, string? statusText = null)
    => _modDetailsProgressTracker.OnRefreshEnqueued(count, statusText);

private void OnModDetailsRefreshCompleted(int completedCount = 1)
    => _modDetailsProgressTracker.OnRefreshCompleted(completedCount);
```

- **Dispose** (553–554): `_modDetailsBusyScope?.Dispose(); _modDetailsBusyScope = null;` → `_modDetailsProgressTracker.ReleaseBusyScope();`

**Specified adaptations (report them all):**
1. UI property writes (`ModDetailsProgress`/`ModDetailsStatusText`) → `_progressChanged` delegate.
2. `UpdateIsLoadingModDetails` calls → `_isLoadingChanged` delegate (dispatcher marshalling stays in the VM implementation).
3. `SetStatus`-based status decisions → `_requestLoadingStatus`/`_requestReadyStatus` delegates with `_isModDetailsStatusActive` read via `Func<bool>` — the *conditions* stay verbatim inside the tracker.
4. `BuildModDetailsLoadingStatusMessage()` fallback inside `UpdateProgress` → `_defaultStageText()`.
5. `BeginBusyScope()` → `_beginBusyScope()`.
6. Method/field renames per the table (drop the `ModDetails` prefix inside the tracker; the class name carries it).

- [ ] **Step 1:** Create `ModDetailsProgressTracker.cs` per the table (bodies verbatim + substitutions).
- [ ] **Step 2:** Rewire `MainViewModel` (field, ctor wiring, pass-throughs, `UpdateLoadedModsStatus`, Dispose). Stale-reference gate:

```bash
grep -n '_pendingModDetailsRefreshCount\|_modDetailsRefreshTotalWork\|_modDetailsRefreshCompletedWork\|_modDetailsProgressStage\|_modDetailsBusyScope\|EnsureModDetailsBusyScope\|ReleaseModDetailsBusyScope\|AddModDetailsWork\|UpdateModDetailsProgress(\|ResetModDetailsProgress\|IsModDetailsRefreshPending' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: **zero hits** (note: `UpdateModDetailsProgressVisibility` is a different method and stays — the pattern above deliberately matches `UpdateModDetailsProgress(` with the paren).

- [ ] **Step 3: Build + verify**

```bash
dotnet build ./ImprovedModMenu.sln --configuration Release
./verify-methods.sh OnRefreshEnqueued OnRefreshCompleted ReleaseBusyScope UpdateModDetailsProgressVisibility UpdateLoadedModsStatus
```

Expected: build clean; each `[OK] ...: 1` except any name colliding with other classes — if `verify-methods.sh` reports >1 for `ReleaseBusyScope`-style generic names, confirm the extra hits are pre-existing in other files, not duplicates you introduced. `OnModDetailsRefreshEnqueued`/`OnModDetailsRefreshCompleted` report 1 each (now pass-throughs).

- [ ] **Step 4: Golden-rule check:** `git diff --name-only` shows only `MainViewModel.cs` + the new service file; grep the four bound property names in `MainViewModel.cs` and confirm their declarations are untouched (`git diff` on those regions is empty).

- [ ] **Step 5: Format + commit**

```bash
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/Services/ModDetailsProgressTracker.cs VintageStoryModManager/ViewModels/MainViewModel.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
git add VintageStoryModManager/Services/ModDetailsProgressTracker.cs VintageStoryModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: extract mod-details progress tracker from MainViewModel (slice 15, area M)"
```

---

### Task 2: Tests

**Files:**
- Create: `VintageStoryModManager.Tests/ModDetailsProgressTrackerTests.cs`

**Interfaces — Consumes:** Task 1's tracker. Build a small recording harness in the test class: a `StubScope : IDisposable` counting disposals, lists capturing `(double, string)` progress reports and `bool` loading changes, counters for loading/ready status requests, and a settable `bool StatusActive` backing the `Func<bool>`. Follow `BusyStateTrackerTests.cs` style.

Test list (~8):

1. `Enqueue_FirstBatch_OpensBusyScope_SetsLoading_RequestsLoadingStatus` — one enqueue of 3: exactly one scope created, `isLoadingChanged` saw `[true]`, one loading-status request, `IsRefreshPending` true.
2. `Enqueue_SecondBatch_WhileStatusActive_NoDuplicateStatusRequest` — enqueue 2 (`StatusActive=false`), set `StatusActive=true`, enqueue 2 more: loading-status requested exactly once for the second batch? No — re-read the moved condition (`newCount == count || !_isModDetailsStatusActive()`): second enqueue with newCount(4) != count(2) and status active → **no** second request. Assert total requests == 1 and only one busy scope ever created.
3. `Enqueue_NonPositiveCount_NoOp` — enqueue 0 and −1: no scope, no callbacks, `IsRefreshPending` false.
4. `Completed_ReportsClampedPercentage` — enqueue 4, complete 1: last progress report ≈ 25.0 and text ends with `"(1/4)"`; complete 1 more → 50.0, `"(2/4)"`.
5. `Completed_ToZero_ClosesScope_ClearsLoading_ResetsProgress` — enqueue 2, complete 2 (`StatusActive=true`): scope disposed exactly once, `isLoadingChanged` ended with `false`, one ready-status request, final progress report `(0, "")`, `IsRefreshPending` false.
6. `Completed_MoreThanPending_ClampsAtZero` — enqueue 1, complete 5: no negative state, scope disposed once, subsequent enqueue starts a fresh cycle (new scope, progress reset).
7. `Enqueue_WithStageText_UsesItInProgressText` — enqueue 3 with `"Checking for mod updates..."`, complete 1: progress text starts with that stage text, not the default; a later enqueue without text keeps the existing stage (verbatim behavior of `_modDetailsProgressStage` — verify against the moved body).
8. `ReleaseBusyScope_Idempotent` — call twice with no enqueue: no throw; after enqueue+release, scope disposed exactly once even if called again.

- [ ] **Step 1:** Write tests; `dotnet test VintageStoryModManager.Tests --filter ModDetailsProgressTrackerTests` → all pass. (Known flake: `*_wpftmp` CS0103 from stale Debug obj — re-run once, then `dotnet clean ... --configuration Debug`.)
- [ ] **Step 2:** Full suite: `dotnet test ./ImprovedModMenu.sln --configuration Release` → all pass.
- [ ] **Step 3: Commit**

```bash
git add VintageStoryModManager.Tests/ModDetailsProgressTrackerTests.cs
git commit -m "test: cover ModDetailsProgressTracker (slice 15, area M)"
```

---

### Task 3: Final gates + report

- [ ] `dotnet build ./ImprovedModMenu.sln --configuration Release` → zero warnings/errors.
- [ ] `dotnet format ./ImprovedModMenu.sln style --include <changed files> --diagnostics IDE0005 --severity info --verify-no-changes` → clean (scoped: solution-wide has known pre-existing drift, ~30 findings in untouched files — not yours to fix).
- [ ] Report: worktree path + branch, commit hashes, full Adaptations list, `MainViewModel.cs` line delta, test names/results, any deviations. Do **not** push; do not touch `CLAUDE.md` or stage `.claude/`.

---

## Smoke test (orchestrator/user, post-merge)

Launch and load a mod folder large enough to trigger detail refreshes: the details progress bar appears with a live `(n/m)` counter and climbs to completion; status bar shows "Loading mod details..." then "Loaded N mods. Mod details up to date."; trigger a manual refresh mid-load and confirm counts keep making sense; confirm the progress bar does NOT appear during a FastCheck tick (the `!IsFastCheckInProgress` visibility rule); close the app mid-refresh — no crash on dispose.
