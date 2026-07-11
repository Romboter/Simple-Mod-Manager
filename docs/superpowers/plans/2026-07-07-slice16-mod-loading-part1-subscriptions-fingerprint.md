# Slice 16 (Area M): Mod Loading Part 1 — Subscription Manager + State Fingerprint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** First of two Mod Loading slices: carve the two self-contained edges off the ~950-line Mod Loading cluster — (A) the per-mod `PropertyChanged` subscription lifecycle (`_installedModSubscriptions`/`_searchResultSubscriptions`, attach/detach, collection-changed fan-out) into `ModListSubscriptionManager`, and (B) the mods-state fingerprint change detection (`_modsStateFingerprint`/`_modsStateLock`, capture/compare/snapshot) into `ModsStateFingerprintTracker`. The repository core (`LoadModsAsync`/`PerformFullReloadAsync`/`ApplyPartialUpdates` and the `_mods`/`_modEntriesBySourcePath`/`_modViewModelsBySourcePath` triad) is **explicitly out of scope** — it's the next Mod Loading slice, to be planned after this and the DB-enrichment work land.

**Architecture:** Both services follow the established delegate-injection pattern (`BusyStateTracker`, `UserReportsCoordinator`). The subscription manager owns set membership and event hook/unhook and subscribes itself to the two collections' `CollectionChanged`; the *routing* of property changes (tag filter refresh, counters, user-report queuing) stays in `MainViewModel` — those handlers are cross-cluster glue by design. The fingerprint tracker owns the capture/compare/store state machine; watcher orchestration (`_modsWatcher`, `_clientSettingsWatcher`) stays in `MainViewModel`.

**Tech Stack:** .NET 8 WPF, xUnit. Test data: `TestData.CreateMod(...)` already constructs real `ModListItemViewModel`s.

## Global Constraints

- **Base commit: `3cc0a3f`** (post-slice-15/18 tip). Re-verified there on 2026-07-07: all cluster members present and structurally identical; table line numbers below were written at `2643d75` and sit ~20 lines lower now — current anchors: fields `_installedModSubscriptions` 47, `_modsStateLock` 56, `_searchResultSubscriptions` 63, `_modsStateFingerprint` 114; coordinator lambdas 164–165; `CollectionChanged +=` 196–197; Dispose unhooks 500–501, detach-alls 505–506; snapshot call sites 1095 (`.ConfigureAwait(true)`) and 1195 (none); `CheckForModStateChangesAsync` 1364 (fingerprint tail starts 1384); `UpdateModsStateSnapshotAsync` 1463; `CaptureModsStateFingerprintAsync` 1484; `OnModsCollectionChanged` 1532 (trailing `ScheduleInstalledTagFilterRefresh(); UpdateActiveCount();` at 1553–1554); `OnSearchResultsCollectionChanged` 1557; `EnumerateModItems` 1580; `AttachInstalledMod` 1591; `DetachInstalledMod` 1598; `DetachAllInstalledMods` 1603; `AttachSearchResult` 1612; `DetachSearchResult` 1621; `DetachAllSearchResults` 1626; `OnInstalledModPropertyChanged` 1635; `OnSearchResultPropertyChanged` 1653. External `CheckForModStateChangesAsync` call site confirmed: `MainWindow.ModsWatcherTimer.cs:41` only. `BatchedObservableCollection<T> : ObservableCollection<T>` confirmed (expected ctor case). Re-run any grep you depend on; greps beat quoted numbers; stop-and-report on contradictions.
- **Golden rule: `MainViewModel`'s public surface does not change.** `CheckForModStateChangesAsync` stays `public` on `MainViewModel` (external callers in `MainWindow.*.cs` — verify with `grep -rn 'CheckForModStateChangesAsync' VintageStoryModManager/Views/ --include='*.cs'` and record the call sites). No `Views/` or `.xaml` files change.
- **Slice-13 integration point:** `UserReportsCoordinator`'s ctor receives `() => _installedModSubscriptions` / `() => _searchResultSubscriptions` (ctor lines 161–162). These lambdas are rewired to the manager's properties — the coordinator itself must NOT be modified.
- **Move, don't rewrite**; enumerate every non-verbatim change under "Adaptations".
- Release build zero warnings/errors; scoped IDE0005 gate on changed files (solution-wide has known pre-existing drift); `./verify-methods.sh` for moved methods.
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` in your own worktree only; verify `VintageStoryModManager/Services/UserReportsCoordinator.cs` exists; stop-and-report on anomalies. Standing permission to abandon a part (A and B are independent — one can land without the other) rather than force it.

---

### Task 1: Extract `ModListSubscriptionManager`

**Files:**
- Create: `VintageStoryModManager/Services/ModListSubscriptionManager.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Pre-step (verify, don't assume):** check `BatchedObservableCollection<T>`'s base type (`grep -n 'class BatchedObservableCollection' -r VintageStoryModManager/ --include='*.cs'`). If it derives from `ObservableCollection<T>` (expected), the ctor below takes `ObservableCollection<ModListItemViewModel>` for both collections. If not, take `IEnumerable<ModListItemViewModel>` and cast to `INotifyCollectionChanged` with a guard throw. Report which case you hit.

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Owns the per-mod PropertyChanged subscription lifecycle for the installed-mods and
///     search-results collections: tracks which view-models are subscribed, hooks/unhooks the
///     owner-supplied handlers as items enter and leave the collections, and re-syncs on Reset.
///     Routing of the property changes themselves stays with the owner.
/// </summary>
public sealed class ModListSubscriptionManager : IDisposable
{
    public ModListSubscriptionManager(
        ObservableCollection<ModListItemViewModel> installedMods,
        ObservableCollection<ModListItemViewModel> searchResults,
        PropertyChangedEventHandler installedModPropertyChanged,   // MainViewModel.OnInstalledModPropertyChanged
        PropertyChangedEventHandler searchResultPropertyChanged,   // MainViewModel.OnSearchResultPropertyChanged
        Action<ModListItemViewModel> onInstalledModAttached,       // owner: user-report queuing side effect
        Action<ModListItemViewModel> onSearchResultAttached,       // owner: user-report queuing side effects
        Action onInstalledModsChanged);                            // owner: tag-filter refresh + active count
    // ctor: ArgumentNullException.ThrowIfNull all seven; store; then
    //   installedMods.CollectionChanged += OnModsCollectionChanged;
    //   searchResults.CollectionChanged += OnSearchResultsCollectionChanged;
    // (replaces MainViewModel ctor lines 193–194)

    public IReadOnlyCollection<ModListItemViewModel> InstalledSubscriptions { get; }     // wraps the HashSet
    public IReadOnlyCollection<ModListItemViewModel> SearchResultSubscriptions { get; }  // wraps the HashSet

    public void Dispose();
    // installedMods.CollectionChanged -= ...; searchResults.CollectionChanged -= ...;
    // DetachAllInstalledMods(); DetachAllSearchResults();
    // (absorbs MainViewModel Dispose lines 502–503 and 507–508)
}
```

**Moves from `MainViewModel` (bodies verbatim + substitutions):**

| Member (line at `2643d75`) | Substitutions |
|---|---|
| `_installedModSubscriptions` (48), `_searchResultSubscriptions` (65) | move as-is (stay `HashSet<ModListItemViewModel>`) |
| `OnModsCollectionChanged` (1552–1575) | Reset branch's `foreach (var mod in _mods)` → `foreach (var mod in _installedMods)` (the injected collection); trailing `ScheduleInstalledTagFilterRefresh(); UpdateActiveCount();` (1573–1574) → `_onInstalledModsChanged();` |
| `OnSearchResultsCollectionChanged` (1577–1598) | Reset branch's `_searchResults` → injected collection; (no trailing calls — keep it that way) |
| `EnumerateModItems` (1600–1607) | move as-is (private static) |
| `AttachInstalledMod` (1611–1616) | `if (_allowModDetailsRefresh) QueueUserReportRefresh(mod);` → `_onInstalledModAttached(mod);` |
| `DetachInstalledMod` (1618–1621), `DetachAllInstalledMods` (1623–1630) | handler references → the injected `installedModPropertyChanged` |
| `AttachSearchResult` (1632–1639) | `if (mod.CanSubmitUserReport) QueueUserReportRefresh(mod); QueueLatestReleaseUserReportRefresh(mod);` → `_onSearchResultAttached(mod);` |
| `DetachSearchResult` (1641–1644), `DetachAllSearchResults` (1646–1653) | handler references → injected `searchResultPropertyChanged` |

**`MainViewModel` keeps:** `OnInstalledModPropertyChanged` (1655–1671) and `OnSearchResultPropertyChanged` (1673–1686) — unchanged, now passed into the manager as the two handler delegates. Also keeps two small private methods for the attach side effects (extracted from the old attach bodies so the conditions stay in the VM where their flags live):

```csharp
private void OnInstalledModAttached(ModListItemViewModel mod)
{
    if (_allowModDetailsRefresh) QueueUserReportRefresh(mod);
}

private void OnSearchResultAttached(ModListItemViewModel mod)
{
    if (mod.CanSubmitUserReport) QueueUserReportRefresh(mod);

    QueueLatestReleaseUserReportRefresh(mod);
}
```

**`MainViewModel` rewires:**
1. New field `private readonly ModListSubscriptionManager _subscriptionManager;`
2. Ctor — construct the manager where lines 193–194 subscribed the events (after `_mods`/`_searchResults` exist, which is at field-initializer time, and **before** the `UserReportsCoordinator` construction so the next step's lambdas are safe):

```csharp
_subscriptionManager = new ModListSubscriptionManager(
    _mods,
    _searchResults,
    OnInstalledModPropertyChanged,
    OnSearchResultPropertyChanged,
    OnInstalledModAttached,
    OnSearchResultAttached,
    () =>
    {
        ScheduleInstalledTagFilterRefresh();
        UpdateActiveCount();
    });
```

3. Coordinator wiring (lines 161–162): `() => _installedModSubscriptions` → `() => _subscriptionManager.InstalledSubscriptions`; same for search results. **If the coordinator construction currently precedes line 193, move the manager construction above it** — the coordinator only stores the lambdas, but keep construction order honest anyway.
4. Dispose: lines 502–503 and 507–508 → `_subscriptionManager.Dispose();`

- [ ] **Step 1:** Pre-step base-type check; create the service.
- [ ] **Step 2:** Rewire `MainViewModel`. Stale-reference gate:

```bash
grep -n '_installedModSubscriptions\|_searchResultSubscriptions\|AttachInstalledMod\|DetachInstalledMod\|DetachAllInstalledMods\|AttachSearchResult\|DetachSearchResult\|DetachAllSearchResults\|EnumerateModItems\|OnModsCollectionChanged\|OnSearchResultsCollectionChanged' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: **zero hits** (the new `OnInstalledModAttached`/`OnSearchResultAttached` names deliberately don't match).

- [ ] **Step 3:** `dotnet build ./ImprovedModMenu.sln --configuration Release` clean; `./verify-methods.sh AttachInstalledMod AttachSearchResult OnModsCollectionChanged OnSearchResultsCollectionChanged` → each `[OK] ...: 1` (now in the service).
- [ ] **Step 4:** Format gate + commit:

```bash
git commit -m "refactor: extract mod-list subscription manager from MainViewModel (slice 16 part A, area M)"
```

---

### Task 2: Extract `ModsStateFingerprintTracker`

**Files:**
- Create: `VintageStoryModManager/Services/ModsStateFingerprintTracker.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Detects out-of-band mod-state changes by fingerprinting the discovery service's view of the
///     mods folder and comparing across polls. Used only when the filesystem watcher isn't active;
///     when it is, the snapshot is cleared so the first post-watcher poll re-baselines instead of
///     reporting a spurious change.
/// </summary>
public sealed class ModsStateFingerprintTracker
{
    public ModsStateFingerprintTracker(
        Func<string?> captureFingerprint,   // () => _discoveryService.GetModsStateFingerprint()
        Func<bool> isWatcherActive);        // () => _modsWatcher.IsWatching

    public Task<bool> HasFingerprintChangedAsync();  // body = MainViewModel.cs:1404–1422 verbatim
    public Task RefreshSnapshotAsync();              // body = UpdateModsStateSnapshotAsync (1483–1502) verbatim
}
```

**Moves (verbatim + substitutions):**

| Member (line) | Substitutions |
|---|---|
| `_modsStateLock` (58), `_modsStateFingerprint` (121) | move as private fields |
| tail of `CheckForModStateChangesAsync` (1404–1422) | becomes `HasFingerprintChangedAsync` — the `CaptureModsStateFingerprintAsync()` call → private `CaptureAsync()`; lock/compare/store logic verbatim |
| `UpdateModsStateSnapshotAsync` (1483–1502) | becomes `RefreshSnapshotAsync` — `_modsWatcher.IsWatching` → `_isWatcherActive()` |
| `CaptureModsStateFingerprintAsync` (1504–1517) | becomes private `CaptureAsync` — `_discoveryService.GetModsStateFingerprint()` → `_captureFingerprint()`; keep the `Task.Run` + catch-all-returns-null shape verbatim |

**`MainViewModel` rewires:**
1. Field `private readonly ModsStateFingerprintTracker _modsStateFingerprintTracker;` + ctor wiring (near the other service constructions):

```csharp
_modsStateFingerprintTracker = new ModsStateFingerprintTracker(
    () => _discoveryService.GetModsStateFingerprint(),
    () => _modsWatcher.IsWatching);
```

2. `CheckForModStateChangesAsync` (1384–1423): keeps the watcher/client-settings orchestration (1386–1402) verbatim — including the `if (_modsWatcher.IsWatching) return false;` early-out — and its tail (1404–1422) becomes:

```csharp
        return await _modsStateFingerprintTracker.HasFingerprintChangedAsync().ConfigureAwait(false);
```

3. The two `UpdateModsStateSnapshotAsync()` call sites (1115, 1215) → `await _modsStateFingerprintTracker.RefreshSnapshotAsync()` with each site's existing `ConfigureAwait` argument preserved exactly (check both: 1115 uses `.ConfigureAwait(true)`, 1215 has none — mirror what's there).

- [ ] **Step 1:** Create the service; rewire. Stale-reference gate:

```bash
grep -n '_modsStateLock\|_modsStateFingerprint\b\|UpdateModsStateSnapshotAsync\|CaptureModsStateFingerprintAsync' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: zero hits except the `_modsStateFingerprintTracker` field/calls (the `\b` keeps the field name from matching the tracker variable — verify visually).

- [ ] **Step 2:** Build clean; `./verify-methods.sh HasFingerprintChangedAsync RefreshSnapshotAsync CheckForModStateChangesAsync` → 1 each.
- [ ] **Step 3:** Format gate + commit:

```bash
git commit -m "refactor: extract mods-state fingerprint tracker from MainViewModel (slice 16 part B, area M)"
```

---

### Task 3: Tests

**Files:**
- Create: `VintageStoryModManager.Tests/ModListSubscriptionManagerTests.cs`
- Create: `VintageStoryModManager.Tests/ModsStateFingerprintTrackerTests.cs`

**ModListSubscriptionManagerTests (~6, using `TestData.CreateMod(...)` and a plain `ObservableCollection<ModListItemViewModel>`):**
1. `Add_AttachesHandler_AndFiresAttachCallback` — add a mod: it appears in `InstalledSubscriptions`, the attach callback saw it, and raising a property change on the mod invokes the injected handler (use a recording `PropertyChangedEventHandler`; trigger via whatever public mutation `TestData` mods support — read `TestData.CreateMod` first and pick an observable property).
2. `Remove_DetachesHandler` — add then remove: gone from the set; a subsequent property change does NOT invoke the handler.
3. `Reset_Resyncs` — add two mods, `collection.Clear()` (Reset), then re-add one: set reflects exactly the current collection contents; no duplicate handler invocations per change (attach is `HashSet.Add`-guarded — assert handler fires exactly once per change).
4. `InstalledChange_FiresChangedCallback_SearchResultsDoNot` — every installed-collection mutation fires `onInstalledModsChanged` once; search-result mutations never fire it.
5. `SearchResultAttach_FiresItsCallback` — add to search results: `onSearchResultAttached` saw it, `onInstalledModAttached` did not.
6. `Dispose_DetachesAll_AndStopsTracking` — dispose, then mutate both collections: sets stay empty, no callbacks fire, property changes on previously-tracked mods don't invoke handlers.

**ModsStateFingerprintTrackerTests (~5, delegates: settable `string? fingerprint` and `bool watching` captured locals):**
1. `FirstCapture_Baselines_ReturnsFalse` — first `HasFingerprintChangedAsync` with "A" → false; second with "A" → false.
2. `ChangedFingerprint_ReturnsTrue_AndRebaselines` — "A" (false), "B" → true, "B" again → false.
3. `NullCapture_ReturnsFalse_KeepsBaseline` — "A" (false), null → false, "B" → true (baseline survived the null).
4. `CaptureThrows_ReturnsFalse` — delegate throws → false, no exception escapes (the moved catch-all is inside the tracker).
5. `RefreshSnapshot_WhileWatching_ClearsBaseline` — "A" baselined; `watching = true`; `RefreshSnapshotAsync()`; `watching = false`; `HasFingerprintChangedAsync` with "A" → false (re-baseline, not change).

- [ ] **Step 1:** Write both test files; run each filter, then full suite `dotnet test ./ImprovedModMenu.sln --configuration Release` → all pass (known `*_wpftmp` flake: re-run once, then `dotnet clean ... --configuration Debug`).
- [ ] **Step 2:** Commit:

```bash
git commit -m "test: cover ModListSubscriptionManager and ModsStateFingerprintTracker (slice 16, area M)"
```

---

### Task 4: Final gates + report

- [ ] Release build zero warnings/errors; scoped IDE0005 clean on all changed files.
- [ ] Golden-rule checks: `git diff --name-only` off base shows only the two service files, `MainViewModel.cs`, and the two test files; the recorded `CheckForModStateChangesAsync` external call sites unchanged.
- [ ] Report: worktree path + branch, commit hashes per task, Adaptations list, `MainViewModel.cs` line delta, `BatchedObservableCollection` base-type finding, test names/results, deviations. No push; no `CLAUDE.md`/`.claude/`.

---

## Smoke test (orchestrator/user, post-merge)

Load mods; toggle a mod's active state → active count and summary text update (installed property-changed routing); enable the tags column → tag filters populate as tags stream in (DatabaseTags routing); install/remove a mod from the Mod Browser while watching the grid (collection add/remove attach paths + user-report queue on attach); with the app running, disable the mods-folder watcher scenario if reachable — otherwise just background-poll: modify a mod file externally and confirm the next watcher/poll cycle picks it up (fingerprint path only runs when the watcher is down, so this may be watcher-driven — either way the refresh must still trigger); restart to confirm no double-subscription weirdness (counts correct immediately after load).

## Follow-up (next Mod Loading slice — not this one)

The repository core: `LoadModsAsync`, `PerformFullReloadAsync`, `ApplyPartialUpdates`, `CreateModViewModel`, `LoadChangedModEntries`, the `_mods`/`_modEntriesBySourcePath`/`_modViewModelsBySourcePath` triad, and selection preservation. Blocked on deciding the enrichment boundary first (`_modEntriesBySourcePath` is written by both clusters — see hot-fields.md). Plan it after this slice and the enrichment slice have landed.
