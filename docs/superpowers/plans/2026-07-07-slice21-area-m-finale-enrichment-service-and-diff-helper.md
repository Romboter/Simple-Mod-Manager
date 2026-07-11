# Slice 21 (Area M finale): ModDatabaseInfoRefreshService + ModEntryDiffHelper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-07-07-area-m-finale-design.md` (approved 2026-07-07) — read it first; its boundary section is binding.

**Goal:** (A) Move the mod-database-info fetch pipeline (~500 lines of orchestration: queue/cancel-restart, progressive/batch refresh flow, batching timer, refresh policy, tag suppression) out of `MainViewModel` into `ModDatabaseInfoRefreshService`; the apply-to-UI bodies stay in the VM as callbacks, keeping `MainViewModel` the single writer of `_modEntriesBySourcePath` and of every `ModEntry`. (B) Extract the pure entry-diff rules into a static `ModEntryDiffHelper`.

**Architecture:** Delegate-injection pattern per `ModUpdatePollingService`/`UserReportsCoordinator`. The service is dispatcher-free by construction: entries in as method arguments, results out through two apply delegates whose implementations (the current `ApplyDatabaseInfoBatchAsync`/`ApplyDatabaseInfoImmediateAsync` bodies) remain in the VM.

**Tech Stack:** .NET 8 WPF, xUnit.

## Global Constraints

- **Base commit: orchestrator-supplied tip** (anchors below verified at `5adbea3`, code identical to `c724a3a`). Re-run every grep; greps beat quoted numbers; stop-and-report on contradictions.
- **THE BOUNDARY RULE (from the spec, binding):** the service file must contain **no assignment to any `ModEntry` member and no reference to `_modEntriesBySourcePath`/`_modViewModelsBySourcePath`**. Step 4 of Task 1 has the grep gate. Reads of `entry.DatabaseInfo`/`entry.ModId`/etc. are fine; writes are not.
- **Golden rule: `MainViewModel`'s public surface does not change.** `RefreshInstalledModDetails` stays `internal` (sole external caller: `MainWindow.ModRefresh.cs:51`). No `Views/` or `.xaml` changes.
- **Move, don't rewrite.** Bodies verbatim except the substitutions specified per task; enumerate every non-verbatim change under "Adaptations" — an unlisted adaptation is a task failure.
- Release build zero warnings/errors. **IDE0005 gate is now solution-wide and unscoped** (post-slice-20): `dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal` must exit clean.
- `verify-methods.sh` is untracked (absent in fresh worktrees) and only scans `MainWindow.*.cs` — use `grep -rn 'Name(' --include='*.cs' VintageStoryModManager/ | grep -v obj/ | grep -v bin/` for method counts.
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` in your own worktree only; base-proof: `VintageStoryModManager/Services/OfflineModDatabaseInfoBuilder.cs` and `docs/superpowers/specs/2026-07-07-area-m-finale-design.md` both exist. Stop-and-report on anomalies. Standing permission to abandon Part B (Task 3) independently; Part A is the slice's core.

**Current anchors (at `5adbea3`):** fields `MaxConcurrentDatabaseRefreshes` 27, `_databaseRefreshLock` 47, `_suppressedTagEntries` 62, `_databaseInfoBatchLock` 68, `_pendingDatabaseInfoUpdates` 69, `_databaseInfoBatchTimer` 70, `DatabaseInfoBatchDelayMs`/`DatabaseInfoBatchSize` 71–72, `_databaseRefreshTask` 104, `_databaseRefreshCts` 105. Methods: `QueueDatabaseInfoRefresh` 2124, `RefreshDatabaseInfoBatchAsync` 2152, `NeedsDatabaseRefresh` 2194, `ShouldSkipOnlineDatabaseRefresh` 2207, `TryGetTagSuppressionKey` 2218, `RefreshDatabaseInfoProgressivelyAsync` 2243, `RefreshDatabaseInfoAsync` 2284, `PopulateOfflineDatabaseInfoAsync` 2449, `PopulateOfflineInfoForEntryAsync` 2474, `ApplyDatabaseInfoAsync` 2493, `ShouldUseBatchedUpdates` 2511, `QueueDatabaseInfoUpdate` 2520, `FlushDatabaseInfoBatch` 2559, `FlushDatabaseInfoBatchLocked` 2572, `ApplyDatabaseInfoBatchAsync` 2587 (STAYS), `ApplyDatabaseInfoImmediateAsync` 2642 (STAYS), `PrepareDatabaseInfoForVisibility` 2692. Queue call sites: 198 (polling ctor lambda), 855, 877, 1096, 1187, 1344, 1586. Dispose teardown: batch block ~530–535, refresh-CTS block ~537–541, task wait ~544–550. Part B: `LoadChangedModEntries` 1971, `ResetCalculatedModState` 1993, `CopyTransientModState` 2000; callers 1052, 1144, 1253, 1255.

---

### Task 1: Extract `ModDatabaseInfoRefreshService`

**Files:**
- Create: `VintageStoryModManager/Services/ModDatabaseInfoRefreshService.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Orchestrates mod-database-info refreshes: filters and queues entries, cancels superseded
///     runs, fetches online info via ModDatabaseService (or offline via OfflineModDatabaseInfoBuilder),
///     batches results on a timer, and hands prepared results to the owner through the two apply
///     delegates. Dispatcher marshalling, entry mutation, and the entry/view-model dictionaries stay
///     with the owner — this service never writes a ModEntry.
/// </summary>
public sealed class ModDatabaseInfoRefreshService : IDisposable
{
    public ModDatabaseInfoRefreshService(
        ModDatabaseService databaseService,
        OfflineModDatabaseInfoBuilder offlineInfoBuilder,
        TimingService timingService,
        Func<string?> installedGameVersionProvider,          // () => InstalledGameVersion
        Func<bool> requireExactVsVersionMatch,               // () => _configuration.RequireExactVsVersionMatch
        Func<bool> allowModDetailsRefresh,                   // () => _allowModDetailsRefresh
        Func<bool> isInitialLoad,                            // () => _isInitialLoad
        Func<bool> isTagsColumnVisible,                      // () => _isTagsColumnVisible
        Func<bool> areUserReportsVisible,                    // () => _userReportsCoordinator.IsVisible
        Action<int> onRefreshEnqueued,                       // count => OnModDetailsRefreshEnqueued(count)
        Action onRefreshCompleted,                           // () => OnModDetailsRefreshCompleted()
        Func<IReadOnlyList<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)>, Task> applyBatchAsync,
        Func<ModEntry, ModDatabaseInfo, bool, Task> applyImmediateAsync);
    // ctor: ArgumentNullException.ThrowIfNull all thirteen; assign to readonly fields.

    public void QueueRefresh(IEnumerable<ModEntry> entries, bool forceRefresh = false);
    public void Dispose();   // moved teardown: cancel/dispose CTS, wait ≤2s on task, dispose batch timer, clear pending list
}
```

(Adaptation note vs the spec: the spec's "one apply-callback... batched or immediate" is realized as **two** delegates because the existing code has two distinct apply bodies with different signatures and await semantics — `FlushDatabaseInfoBatchLocked` fires the batch delegate fire-and-forget (`_ = _applyBatchAsync(batch)`), while `ApplyDatabaseInfoAsync` awaits the immediate delegate. List this under Adaptations.)

**Moves (bodies verbatim; substitutions per table):**

| Member (line) | Substitutions |
|---|---|
| Fields `_databaseRefreshLock` 47, `_suppressedTagEntries` 62, `_databaseInfoBatchLock` 68, `_pendingDatabaseInfoUpdates` 69, `_databaseInfoBatchTimer` 70, `_databaseRefreshTask` 104, `_databaseRefreshCts` 105; consts `DatabaseInfoBatchDelayMs`/`DatabaseInfoBatchSize` 71–72; static field `MaxConcurrentDatabaseRefreshes` 27 (keeps its `DevConfig` initializer) | move as-is |
| `QueueDatabaseInfoRefresh` (2124) → public `QueueRefresh` | `_allowModDetailsRefresh` → `_allowModDetailsRefresh()`; `OnModDetailsRefreshEnqueued(pending.Length)` → `_onRefreshEnqueued(pending.Length)` |
| `RefreshDatabaseInfoBatchAsync` (2152) | none (`InternetAccessManager` static stays a direct read) |
| `NeedsDatabaseRefresh` (2194) | `_isTagsColumnVisible` → `_isTagsColumnVisible()` |
| `ShouldSkipOnlineDatabaseRefresh` (2207) | `_isTagsColumnVisible` → `_isTagsColumnVisible()`; `_userReportsCoordinator.IsVisible` → `_areUserReportsVisible()` |
| `TryGetTagSuppressionKey` (2218) | none (private static, moves whole — its 2 remaining VM callers are inside `PrepareDatabaseInfoForVisibility`, also moving; re-grep to confirm no others appeared) |
| `RefreshDatabaseInfoProgressivelyAsync` (2243) | none |
| `RefreshDatabaseInfoAsync` (2284) | `InstalledGameVersion` → `_installedGameVersionProvider()` (3 sites); `_configuration.RequireExactVsVersionMatch` → `_requireExactVsVersionMatch()` (3 sites); `_isInitialLoad` → `_isInitialLoad()`; `OnModDetailsRefreshCompleted()` → `_onRefreshCompleted()`; `_databaseService`/`_timingService` stay as the injected instances (`StatusLogService` static stays) |
| `PopulateOfflineDatabaseInfoAsync` (2449) | `OnModDetailsRefreshCompleted()` → `_onRefreshCompleted()` |
| `PopulateOfflineInfoForEntryAsync` (2474) | `InstalledGameVersion`/`_configuration.RequireExactVsVersionMatch` → the two delegates; `_offlineInfoBuilder` → the injected instance (static `OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo` call unchanged) |
| `ApplyDatabaseInfoAsync` (2493) | `_isInitialLoad` → `_isInitialLoad()`; the final line `await ApplyDatabaseInfoImmediateAsync(entry, preparedInfo, loadLogoImmediately).ConfigureAwait(false);` → `await _applyImmediateAsync(entry, preparedInfo, loadLogoImmediately).ConfigureAwait(false);` |
| `ShouldUseBatchedUpdates` (2511), `QueueDatabaseInfoUpdate` (2520), `FlushDatabaseInfoBatch` (2559) | none |
| `FlushDatabaseInfoBatchLocked` (2572) | `_ = ApplyDatabaseInfoBatchAsync(batch);` → `_ = _applyBatchAsync(batch);` |
| `PrepareDatabaseInfoForVisibility` (2692) | `_isTagsColumnVisible` → `_isTagsColumnVisible()` |
| Dispose teardown blocks (~530–550: batch-lock block, refresh-lock block, task-wait block) | become the service's `Dispose()` body, verbatim |

**STAYS in `MainViewModel` (the apply callbacks + glue):**
- `ApplyDatabaseInfoBatchAsync` (2587) and `ApplyDatabaseInfoImmediateAsync` (2642) — unchanged bodies; they are the only code that writes entries (`currentEntry.UpdateDatabaseInfo`), reads the two dictionaries, and marshals to the dispatcher.
- Private pass-through so all 7 queue call sites (198, 855, 877, 1096, 1187, 1344, 1586) stay textually unchanged:

```csharp
private void QueueDatabaseInfoRefresh(IEnumerable<ModEntry> entries, bool forceRefresh = false)
    => _databaseInfoRefreshService.QueueRefresh(entries, forceRefresh);
```

- `RefreshInstalledModDetails` (874, internal) — unchanged (calls the pass-through).
- New field + ctor wiring (place near the other service constructions, AFTER `_offlineInfoBuilder`, `_userReportsCoordinator`, and `_timingService` are initialized — verify order by reading the ctor):

```csharp
private readonly ModDatabaseInfoRefreshService _databaseInfoRefreshService;
// ctor:
_databaseInfoRefreshService = new ModDatabaseInfoRefreshService(
    _databaseService,
    _offlineInfoBuilder,
    _timingService,
    () => InstalledGameVersion,
    () => _configuration.RequireExactVsVersionMatch,
    () => _allowModDetailsRefresh,
    () => _isInitialLoad,
    () => _isTagsColumnVisible,
    () => _userReportsCoordinator.IsVisible,
    count => OnModDetailsRefreshEnqueued(count),
    () => OnModDetailsRefreshCompleted(),
    ApplyDatabaseInfoBatchAsync,
    ApplyDatabaseInfoImmediateAsync);
```

  (If `ApplyDatabaseInfoBatchAsync`'s parameter type `List<...>` doesn't convert to the delegate's `IReadOnlyList<...>`, change the VM method's parameter to `IReadOnlyList<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)>` — it only enumerates and reads `.Count`; list as an Adaptation.)
- `Dispose` (~530–550): the three moved teardown blocks are replaced by `_databaseInfoRefreshService.Dispose();` placed where the batch-lock block was (teardown order relative to the other disposals preserved).

- [ ] **Step 1: Pre-move greps** (stop-and-report on surprises):

```bash
grep -n 'QueueDatabaseInfoRefresh(' VintageStoryModManager/ViewModels/MainViewModel.cs   # expect: definition + 7 call sites
grep -rn 'FlushDatabaseInfoBatch\|_databaseRefreshTask\|_databaseRefreshCts\|_pendingDatabaseInfoUpdates\|_suppressedTagEntries' VintageStoryModManager/ --include='*.cs' | grep -v obj/ | grep -v 'ViewModels/MainViewModel.cs'   # expect: no hits
grep -n 'TryGetTagSuppressionKey' VintageStoryModManager/ViewModels/MainViewModel.cs   # expect: definition + callers only inside NeedsDatabaseRefresh/PrepareDatabaseInfoForVisibility
```

- [ ] **Step 2: Create the service; move members per the table; rewire the VM** (field, ctor wiring, pass-through, Dispose).
- [ ] **Step 3: Stale-reference gate** on the VM:

```bash
grep -n '_databaseRefreshLock\|_databaseRefreshCts\|_databaseRefreshTask\|_databaseInfoBatchLock\|_pendingDatabaseInfoUpdates\|_databaseInfoBatchTimer\|_suppressedTagEntries\|DatabaseInfoBatchSize\|DatabaseInfoBatchDelayMs\|MaxConcurrentDatabaseRefreshes\|RefreshDatabaseInfoBatchAsync\|RefreshDatabaseInfoProgressivelyAsync\|RefreshDatabaseInfoAsync\|PopulateOfflineDatabaseInfoAsync\|PopulateOfflineInfoForEntryAsync\|ShouldUseBatchedUpdates\|QueueDatabaseInfoUpdate\|FlushDatabaseInfoBatch\|NeedsDatabaseRefresh\|ShouldSkipOnlineDatabaseRefresh\|TryGetTagSuppressionKey\|PrepareDatabaseInfoForVisibility\|ApplyDatabaseInfoAsync' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: only the `_databaseInfoRefreshService` field/ctor/pass-through/Dispose lines and the two staying methods `ApplyDatabaseInfoBatchAsync`/`ApplyDatabaseInfoImmediateAsync` (whose names deliberately don't match any pattern above — the bare names listed must have zero hits).

- [ ] **Step 4: THE BOUNDARY GATE** on the service file:

```bash
grep -nE '\bentry\.[A-Za-z]+\s*=|currentEntry|UpdateDatabaseInfo|_modEntriesBySourcePath|_modViewModelsBySourcePath|InvokeOnDispatcherAsync|Dispatcher' VintageStoryModManager/Services/ModDatabaseInfoRefreshService.cs
```

Expected: **zero hits.** Any hit means apply/dispatcher/dictionary code leaked into the service — stop and fix before proceeding.

- [ ] **Step 5: Build + verify:** `dotnet build ./ImprovedModMenu.sln --configuration Release` → 0/0; method-count grep: each moved method defined exactly once, in the service; `QueueDatabaseInfoRefresh` reports 2 (pass-through + `QueueRefresh` is a different name — confirm the pass-through body is the only definition in the VM).
- [ ] **Step 6: Format gate + commit:**

```bash
dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
git add VintageStoryModManager/Services/ModDatabaseInfoRefreshService.cs VintageStoryModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: extract mod-database-info refresh service from MainViewModel (slice 21 part A, area M)"
```

---

### Task 2: Service tests

**Files:**
- Create: `VintageStoryModManager.Tests/ModDatabaseInfoRefreshServiceTests.cs`

**Interfaces — Consumes:** Task 1's service. Harness: settable captured locals for the six `Func<bool>`/`Func<string?>` gates; recording lists for enqueued counts, completed calls, applied batches, applied immediates; a real `TimingService` instance (cheap to construct — check its ctor; if not, stop and report rather than mocking around it). `ModDatabaseService` is the hard dependency — check whether it can be constructed in tests with fetches that fail fast offline (the offline/error paths need no network). If `ModDatabaseService` cannot be safely constructed in a test process, **stop and report** the constraint with a recommendation (e.g. seam extraction as a follow-up) rather than improvising a network-touching test. Entries via `TestData`/direct `ModEntry` construction with no cache directory (slice-17 precedent: use mod ids guaranteed to have no cache).

Test list (~10 — read each moved body before asserting; the moved code is the truth):

1. `QueueRefresh_GateClosed_NoForce_NoOp` — `allowModDetailsRefresh = false`, force false: no enqueue callback, no task.
2. `QueueRefresh_GateClosed_Force_Runs` — force true bypasses the gate: enqueue callback fired with the filtered count.
3. `QueueRefresh_FiltersNullAndBlankModIds` — entries `[null, blank-id, good]` force-queued → enqueued count 1.
4. `QueueRefresh_NoForce_UsesNeedsDatabaseRefresh` — entry with non-offline `DatabaseInfo` set → filtered out; entry with null info → kept.
5. `NeedsRefresh_SuppressedTagEntry_TagsVisible_ReturnsTrue` — seed suppression by applying with tags hidden, then flip `isTagsColumnVisible = true` and re-queue without force → entry passes the filter (exercises `_suppressedTagEntries` round-trip through `PrepareDatabaseInfoForVisibility` + `NeedsDatabaseRefresh`).
6. `Requeue_CancelsPriorRun` — queue a large batch, immediately re-queue: first run's completions stop, total completed calls ≤ total enqueued, no double-apply for the same entry after the second run finishes (poll with `SemaphoreSlim`/short-delay loop, no long sleeps).
7. `Offline_PopulatesViaBuilder_AppliesMerged` — `InternetAccessManager` disabled state (set + restore in finally, static — same handling as `ModUpdatePollingServiceTests`; read those first): apply callback receives info for an entry, completed fired per entry.
8. `InitialLoad_RoutesToBatch` — `isInitialLoad = true`: applies arrive via the batch delegate, not the immediate one; final flush delivers leftovers.
9. `PostInitial_SmallSet_RoutesToImmediate` — `isInitialLoad = false`, empty pending queue: immediate delegate used.
10. `Dispose_MidRun_StopsCleanly` — queue then dispose: no callbacks after dispose returns (allowing the ≤2s wait), no unobserved exceptions, second dispose no-throw.

- [ ] **Step 1:** Write tests; `dotnet test VintageStoryModManager.Tests --filter ModDatabaseInfoRefreshServiceTests` → all pass. (Known `*_wpftmp` flake: re-run once, then `dotnet clean ./ImprovedModMenu.sln --configuration Debug`.)
- [ ] **Step 2:** Full suite Release → all pass.
- [ ] **Step 3: Commit:**

```bash
git add VintageStoryModManager.Tests/ModDatabaseInfoRefreshServiceTests.cs
git commit -m "test: cover ModDatabaseInfoRefreshService (slice 21 part A, area M)"
```

---

### Task 3: Extract `ModEntryDiffHelper` (Part B — abandonable independently)

**Files:**
- Create: `VintageStoryModManager/Services/ModEntryDiffHelper.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`
- Create: `VintageStoryModManager.Tests/ModEntryDiffHelperTests.cs`

**Scout finding (done at plan time — verify, don't re-derive):** `LoadChangedModEntries` (1971) is pure except one call: `_discoveryService.LoadModFromPath(path)` — becomes a `Func<string, ModEntry?>` parameter. `ResetCalculatedModState` (1993) and `CopyTransientModState` (2000) are fully pure statics.

**Interfaces — Produces:**

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Pure entry-diff rules for incremental mod reloads: reload changed paths through the supplied
///     loader, reset per-load calculated state, and carry transient state (database info, search
///     score) across an entry swap when the mod identity is unchanged.
/// </summary>
internal static class ModEntryDiffHelper
{
    internal static Dictionary<string, ModEntry?> LoadChangedModEntries(
        IReadOnlyCollection<string> paths,
        IReadOnlyDictionary<string, ModEntry>? existingEntries,
        Func<string, ModEntry?> loadModFromPath);   // body verbatim from MainViewModel:1971 with _discoveryService.LoadModFromPath → loadModFromPath

    internal static void ResetCalculatedModState(ModEntry entry);        // verbatim from 1993
    internal static void CopyTransientModState(ModEntry source, ModEntry target); // verbatim from 2000
}
```

**VM rewires:** call sites 1052 and 1144 become `ModEntryDiffHelper.LoadChangedModEntries(candidates_or_paths, existingEntriesSnapshot, _discoveryService.LoadModFromPath)` (keep each site's exact existing arguments for the first two parameters); `ApplyPartialUpdates`' inline calls at 1253/1255 become `ModEntryDiffHelper.ResetCalculatedModState(entry)` / `ModEntryDiffHelper.CopyTransientModState(previous, entry)`. Delete the three VM methods.

Test list (~4):
1. `LoadChangedModEntries_LoaderNull_EntryRecordedAsNull` — loader returns null for a path → `results[path] == null`.
2. `LoadChangedModEntries_ResetsCalculatedState_AndCopiesTransient` — existing entry same ModId+Version with `DatabaseInfo` set; loader returns a fresh entry with `LoadError` set → result has `LoadError == null`, `DatabaseInfo` carried over.
3. `CopyTransientModState_DifferentIdentity_NoCopy` — different ModId (or different Version) → `DatabaseInfo` NOT copied; both-blank versions count as same.
4. `CopyTransientModState_TargetInfoWins` — target already has `DatabaseInfo` → source's not copied; `ModDatabaseSearchScore` copied only when source has a value.

- [ ] **Step 1:** Re-verify the scout greps: `grep -n 'LoadChangedModEntries\|ResetCalculatedModState\|CopyTransientModState' VintageStoryModManager/ViewModels/MainViewModel.cs` → definitions at ~1971/1993/2000 + callers at ~1052/1144/1253/1255 and inside `LoadChangedModEntries` itself; nothing else.
- [ ] **Step 2:** Create helper, rewire, delete VM methods. Stale grep: the same grep → only the four rewired `ModEntryDiffHelper.`-prefixed call sites.
- [ ] **Step 3:** Build 0/0; write tests; filter run → pass; full suite → pass.
- [ ] **Step 4: Commit:**

```bash
git add VintageStoryModManager/Services/ModEntryDiffHelper.cs VintageStoryModManager/ViewModels/MainViewModel.cs VintageStoryModManager.Tests/ModEntryDiffHelperTests.cs
git commit -m "refactor: extract mod-entry diff helper from MainViewModel (slice 21 part B, area M)"
```

---

### Task 4: Final gates + report

- [ ] `dotnet build ./ImprovedModMenu.sln --configuration Release` → 0/0.
- [ ] Solution-wide unscoped IDE0005 `--verify-no-changes` → clean (this is now the standard gate).
- [ ] Full suite Release → all pass.
- [ ] `git diff --name-only` off base: exactly the two new service files, `MainViewModel.cs`, and the two test files (minus Part B's if abandoned).
- [ ] Re-run the Task 1 Step 4 boundary gate one final time → zero hits.
- [ ] Report: worktree path + branch, commit hashes per task, full Adaptations list (Parts A and B separately), `MainViewModel.cs` line delta (expect roughly −500 for A, −45 for B), the `ModDatabaseService`-in-tests finding (constructible or stop-and-report outcome), test names/results, deviations. No push; no `CLAUDE.md`/`.claude/`.

---

## Smoke test (orchestrator/user, post-merge)

Initial load with many mods: details progress bar climbs, tags/downloads stream into the grid in batches (batching path), no stutter. Toggle tags column off → newly refreshed rows show no tags; toggle back on → suppressed rows refresh and tags appear (suppression round-trip). Trigger Refresh (force) mid-initial-load → prior run cancels cleanly, counts stay sane, no duplicate-looking updates. Offline mode → cached/offline data still populates (slice-17 path through the new orchestration). Single-mod refresh after startup → immediate (non-batched) update lands promptly. Exit the app mid-refresh → clean shutdown, no crash, no hang beyond ~2s.
