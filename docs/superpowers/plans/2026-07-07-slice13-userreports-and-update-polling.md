# Slice 13 (Area M): UserReportsCoordinator + ModUpdatePollingService Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the user-report/vote cluster (~500 lines) and the FastCheck update-polling cluster (~200 lines) out of `MainViewModel` into two new services, after first deleting the dead vote-change sweep that appeared to couple them.

**Architecture:** `UserReportsCoordinator` owns vote fetch/submit/etag-cache/operation-counting end to end, with busy-scope, game-version, and subscription access injected as narrow delegates. `ModUpdatePollingService` owns the FastCheck timer/re-entrancy machine with snapshot/fetch/callback delegates. **They are independent** — the etag writes that would have coupled them live only in `CheckForVoteChangesAsync`/`CheckVoteChangesForModAsync`, which have zero callers (dead code, verified; delete first, Task 1).

**Tech Stack:** .NET 8 WPF, xUnit (existing `VintageStoryModManager.Tests`, has `InternalsVisibleTo`).

## Global Constraints

- **Golden rule: `MainViewModel`'s public surface does not change.** Everything `MainWindow.*.cs` or XAML touches stays as a public member (pass-through where the body moved). Final gate greps below verify this.
- **The nested type `MainViewModel.ModUserReportChangedEventArgs` stays nested in `MainViewModel`** — `MainWindow.ModBrowser.cs:11` aliases it (`using ModUserReportChangedEventArgs = ...MainViewModel.ModUserReportChangedEventArgs;`). Do not move, rename, or un-nest it.
- **Move, don't rewrite.** Bodies transfer verbatim except for the delegate substitutions specified per task. **Enumerate every non-verbatim change** in your final report under an "Adaptations" heading — one line each. An unlisted adaptation is a task failure.
- Release build must pass with **zero warnings, zero errors**: `dotnet build ./ImprovedModMenu.sln --configuration Release`.
- After each task: `dotnet format ./ImprovedModMenu.sln style --include <changed files> --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal` must exit clean.
- Method-count verification uses `./verify-methods.sh Name1 Name2 ...` (repo root) — every moved method must report `[OK] Name: 1`.
- **Standing permission to abandon:** if any part turns out tangled beyond this spec, stop that part, report why, and leave the tree at the last green commit. Do not force it.
- **Worktree environment check (first action, before any edit):** `git rev-parse --show-toplevel` must resolve under `.claude/worktrees/`. Then `git reset --hard <TIP-COMMIT>` (orchestrator supplies the hash) **inside your own worktree only**. If either looks wrong — cwd resolves to `Simple-Mod-Manager-Refactor`, branch looks unexpected, files missing — **stop and report; never touch the shared worktree.**
- Reference file: `C:\Users\Jason\AppData\Local\Temp\claude\C--sc-games-Simple-Mod-Manager-Refactor\d59d070a-ad96-43ad-9d0b-ea30496121ba\scratchpad\UserReportsCoordinator.partial.cs` is a well-formed prior draft of the coordinator (a previous attempt failed on call-site rewiring, not on this file). Use it as the reference for method bodies; apply the API changes Task 2 specifies (it was drafted before the dead-sweep discovery).

---

### Task 1: Delete the dead vote-change sweep and dead progress path

**Files:**
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces:** Produces a `MainViewModel` where the only FastCheck→user-report coupling is gone, so Tasks 2 and 3 are independent.

- [ ] **Step 1: Verify the code is dead** (must be re-verified in your worktree, not trusted from this plan):

```bash
grep -rn 'CheckForVoteChangesAsync' --include='*.cs' VintageStoryModManager/ | grep -v 'private async Task CheckForVoteChangesAsync'
grep -rn 'CheckVoteChangesForModAsync' --include='*.cs' VintageStoryModManager/ | grep -v MainViewModel.cs
grep -rn 'VoteCheckTarget' --include='*.cs' VintageStoryModManager/ | grep -v MainViewModel.cs
grep -rn 'CheckForNewModReleasesAsync' --include='*.cs' VintageStoryModManager/ | grep -v obj/ | grep -v bin/
```

Expected: first three produce **no source hits** (definitions/dead-internal uses only); the fourth shows exactly one call site, `MainViewModel.cs:648`, passing `showProgress: false`. If any expectation fails, **stop Task 1 and report** — the deletion premise is wrong.

- [ ] **Step 2: Delete** from `MainViewModel.cs`:
  - `CheckForVoteChangesAsync` (line ~780)
  - `CheckVoteChangesForModAsync` (line ~818)
  - `private sealed record VoteCheckTarget(...)` (line ~4352)
  - The `showProgress` parameter of `CheckForNewModReleasesAsync` and its dead branch: the `bool showProgress = true` parameter, the `if (showProgress) { OnModDetailsRefreshEnqueued(...); modDetailsCheckEnqueued = true; }` block, the `modDetailsCheckEnqueued` local, and the `finally` block's `if (modDetailsCheckEnqueued) { ... OnModDetailsRefreshCompleted(...); }` (the `completedChecks` local goes too if now unused). Caller at line 648 drops its `false` argument.

- [ ] **Step 3: Build + verify**

```bash
dotnet build ./ImprovedModMenu.sln --configuration Release
./verify-methods.sh CheckForNewModReleasesAsync RunFastCheckAsync
```

Expected: build clean; both methods still `[OK] ...: 1`. Also confirm zero remaining references: `grep -n 'CheckForVoteChangesAsync\|CheckVoteChangesForModAsync\|VoteCheckTarget\|showProgress' VintageStoryModManager/ViewModels/MainViewModel.cs` → no hits.

- [ ] **Step 4: Commit**

```bash
git add VintageStoryModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: delete dead vote-change sweep and dead progress path (slice 13 prep, area M)"
```

---

### Task 2: Extract `UserReportsCoordinator`

**Files:**
- Create: `VintageStoryModManager/Services/UserReportsCoordinator.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
public sealed class UserReportsCoordinator : IDisposable
{
    public UserReportsCoordinator(
        string voteEtagCachePath,
        Func<IDisposable> beginBusyScope,                                    // MainViewModel.BeginBusyScope
        Func<string?> installedGameVersionProvider,                          // () => InstalledGameVersion
        Func<IEnumerable<ModListItemViewModel>> installedModSubscriptionsProvider,
        Func<IEnumerable<ModListItemViewModel>> searchResultSubscriptionsProvider);

    public event EventHandler<MainViewModel.ModUserReportChangedEventArgs>? UserReportVoteSubmitted;

    public bool SetVisibility(bool isVisible);          // returns true when the flag changed
    public void QueueUserReportRefresh(ModListItemViewModel mod);
    public void QueueLatestReleaseUserReportRefresh(ModListItemViewModel mod);
    public Task<ModVersionVoteSummary?> RefreshUserReportAsync(ModListItemViewModel mod, CancellationToken cancellationToken = default);
    public Task<ModVersionVoteSummary?> RefreshLatestReleaseUserReportAsync(ModListItemViewModel mod, CancellationToken cancellationToken = default);
    public void EnableUserReportFetching(bool includeInstalledWhenAutoRefreshDisabled = false);
    public Task<ModVersionVoteSummary?> SubmitUserReportVoteAsync(ModListItemViewModel mod, ModVersionVoteOption option, string? comment);
    internal void StoreUserReportEtag(string modId, string? modVersion, string? etag);          // internal for tests
    internal void StoreLatestReleaseUserReportEtag(string modId, string? modVersion, string? etag);
    internal static string BuildVoteEtagKey(string prefix, string modId, string modVersion, string? gameVersion); // internal for tests
    public void Dispose();
}
```

**Moves from `MainViewModel` (bodies verbatim per the reference draft):** fields `_userReportEtags`, `_latestReleaseUserReportEtags`, `_voteEtagCachePath`, `_voteEtagPersistenceLock`, `_userReportOperationLock`, `_activeUserReportOperations`, `_userReportRefreshLimiter`, `_voteService`, `_areUserReportsVisible`, `_hasEnabledUserReportFetching`, `_hasFetchedUserReportsThisSession`, const `MaxConcurrentUserReportRefreshes`; methods `QueueUserReportRefresh`, `QueueLatestReleaseUserReportRefresh`, `RefreshUserReportAsync`, `RefreshUserReportCoreAsync`, `RefreshLatestReleaseUserReportAsync`, `RefreshLatestReleaseUserReportCoreAsync`, `EnableUserReportFetching`, `SubmitUserReportVoteAsync`, `RaiseUserReportVoteSubmitted`, `RunUserReportOperationAsync`, `Begin`/`EndUserReportOperation`, `StoreUserReportEtag`, `StoreLatestReleaseUserReportEtag`, `GetVoteEtag`, `UpdateVoteEtag`, `BuildVoteEtagKey`, `LoadVoteEtagsFromDisk`, `PersistVoteEtagsLocked`, nested `VoteEtagCacheState` + `UserReportOperationScope`.

**Specified adaptations (the only allowed non-verbatim changes; still list them in your report):**
1. `InstalledGameVersion` reads → `_installedGameVersionProvider()`.
2. `BeginBusyScope()` → `_beginBusyScope()`.
3. `_installedModSubscriptions`/`_searchResultSubscriptions` iteration in `EnableUserReportFetching` → the two provider delegates.
4. `RaiseUserReportVoteSubmitted` raises the coordinator's own event (sender = coordinator; MainViewModel re-raises with itself as sender — see wiring below).
5. Unlike the reference draft: `StoreUserReportEtag`/`StoreLatestReleaseUserReportEtag`/`BuildVoteEtagKey` are **internal** (tests only, dead sweep is gone), and the draft's `VoteService` property and `TryGetRawUserReportEtag`/`TryGetRawLatestReleaseUserReportEtag` members are **omitted entirely** — nothing needs them anymore.
6. `InvokeOnDispatcherAsync` calls inside moved bodies: `MainViewModel`'s private static helper — copy it into the coordinator as a private static method, verbatim (same approach `ModListItemViewModel.cs:2277` already uses).

**`MainViewModel` keeps (public surface unchanged):**
- `public event EventHandler<ModUserReportChangedEventArgs>? UserReportVoteSubmitted;` — wired in ctor: `_userReportsCoordinator.UserReportVoteSubmitted += (_, args) => UserReportVoteSubmitted?.Invoke(this, args);`
- Pass-throughs, bodies one line each delegating to `_userReportsCoordinator`: `EnableUserReportFetching(bool includeInstalledWhenAutoRefreshDisabled = false)`, `RefreshUserReportAsync`, `RefreshLatestReleaseUserReportAsync`, `SubmitUserReportVoteAsync`, plus private `QueueUserReportRefresh`/`QueueLatestReleaseUserReportRefresh` pass-throughs for internal call sites (lines ~2017, 2038, 2040, 2087, 4183).
- Private `SetUserReportsColumnVisibility(bool isVisible)` becomes: `if (_userReportsCoordinator.SetVisibility(isVisible) && isVisible && _allowModDetailsRefresh) EnableUserReportFetching();` — preserving the original's exact re-enable condition (check the original at line ~2491 and mirror its logic precisely; if it differs from this sketch, the original wins).
- Nested `ModUserReportChangedEventArgs` class — untouched, stays at line ~4304.
- Ctor wiring:

```csharp
_userReportsCoordinator = new UserReportsCoordinator(
    Path.Combine(DataDirectory, "voteEtags.json"),
    BeginBusyScope,
    () => InstalledGameVersion,
    () => _installedModSubscriptions,
    () => _searchResultSubscriptions);
```

replacing the removed `_voteEtagCachePath` init, `LoadVoteEtagsFromDisk()` call, and `_hasEnabledUserReportFetching = FirebaseAnonymousAuthenticator.HasPersistedState();` line (the coordinator's ctor does that itself).
- `Dispose()`: `_userReportRefreshLimiter.Dispose(); _voteService.Dispose();` → `_userReportsCoordinator.Dispose();`.

- [ ] **Step 1: Create the service file** from the reference draft + adaptations above.
- [ ] **Step 2: Rewire `MainViewModel`** — remove moved members, add field + ctor wiring + pass-throughs. Then hunt every stale reference:

```bash
grep -n '_userReportEtags\|_latestReleaseUserReportEtags\|_voteEtagCachePath\|_voteEtagPersistenceLock\|_userReportOperationLock\|_activeUserReportOperations\|_userReportRefreshLimiter\|_voteService\|_areUserReportsVisible\|_hasEnabledUserReportFetching\|_hasFetchedUserReportsThisSession' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: **zero hits.** The prior attempt failed exactly here — it left ~60 stale references. Do not trust the build alone; run this grep.

- [ ] **Step 3: Build + verify**

```bash
dotnet build ./ImprovedModMenu.sln --configuration Release
./verify-methods.sh QueueUserReportRefresh QueueLatestReleaseUserReportRefresh RefreshUserReportAsync RefreshUserReportCoreAsync RefreshLatestReleaseUserReportAsync RefreshLatestReleaseUserReportCoreAsync SubmitUserReportVoteAsync RunUserReportOperationAsync LoadVoteEtagsFromDisk PersistVoteEtagsLocked
```

Note: the pass-throughs mean `RefreshUserReportAsync`/`RefreshLatestReleaseUserReportAsync`/`SubmitUserReportVoteAsync`/`EnableUserReportFetching`/`QueueUserReportRefresh`/`QueueLatestReleaseUserReportRefresh` legitimately report `2` (facade + implementation). Everything else must be `1`.

- [ ] **Step 4: Golden-rule grep** — every external call site still valid:

```bash
grep -rn 'EnableUserReportFetching\|SubmitUserReportVoteAsync\|RefreshUserReportAsync\|UserReportVoteSubmitted\|ModUserReportChangedEventArgs' VintageStoryModManager/Views/ --include='*.cs'
```

Expected: same call sites as before (`MainWindow.ModBrowser.cs`, `MainWindow.ModRefresh.cs`, `MainWindow.ModUsage.cs`, `MainWindow.UserReports.cs`, `MainWindow.ViewModel.cs`), all compiling against `_viewModel` unchanged.

- [ ] **Step 5: Format check + commit**

```bash
dotnet format ./ImprovedModMenu.sln style --include VintageStoryModManager/Services/UserReportsCoordinator.cs VintageStoryModManager/ViewModels/MainViewModel.cs --diagnostics IDE0005 --severity info --verify-no-changes --verbosity minimal
git add VintageStoryModManager/Services/UserReportsCoordinator.cs VintageStoryModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: extract UserReportsCoordinator from MainViewModel (slice 13 part 1, area M)"
```

---

### Task 3: Extract `ModUpdatePollingService`

**Files:**
- Create: `VintageStoryModManager/Services/ModUpdatePollingService.cs`
- Modify: `VintageStoryModManager/ViewModels/MainViewModel.cs`

**Interfaces — Produces:**

```csharp
public sealed class ModUpdatePollingService : IDisposable
{
    public ModUpdatePollingService(
        TimeSpan interval,                                                    // FastCheckInterval (2 min)
        Func<bool> isAutoRefreshDisabled,                                     // () => _isAutoRefreshDisabled (mutable — line 1276)
        Func<CancellationToken, Task<List<ModEntry>>> snapshotProvider,       // dispatcher-marshalled by the caller
        Func<string, CancellationToken, Task<string?>> fetchLatestReleaseVersionAsync, // _databaseService.TryFetchLatestReleaseVersionAsync
        Action<IReadOnlyList<ModEntry>> onUpdateCandidates,                   // entries => QueueDatabaseInfoRefresh(entries, true)
        Action<bool> onInProgressChanged);                                    // inProgress => IsFastCheckInProgress = inProgress

    public void FastCheck();
    public void ResetTimer();
    public void StopTimer();
    public void Dispose();     // StopTimer + timer.Dispose
    internal static bool IsDifferentVersion(string? installedVersion, string? latestVersion); // internal for tests
}
```

**Moves from `MainViewModel` (verbatim + delegate substitutions):** fields `_fastCheckTimer`, `_fastCheckTimerLock`, `_hasPendingFastCheck`, `_isFastCheckRunning`, const `FastCheckInterval`; methods `FastCheck` (body → service, line 623), `RunFastCheckAsync`, `ResetFastCheckTimer`→`ResetTimer`, `StopFastCheckTimer`→`StopTimer`, `OnFastCheckTimerElapsed`, `CheckForNewModReleasesAsync` (post-Task-1 shape), `IsDifferentVersion`.

**Specified adaptations:**
1. `_isAutoRefreshDisabled` reads → `_isAutoRefreshDisabled()` delegate.
2. `_disposed` gates (lines 670, 690) → the service's own `_disposed` flag, set in `Dispose()`.
3. `IsFastCheckInProgress = x` → `_onInProgressChanged(x)`.
4. The `InvokeOnDispatcherAsync` snapshot block inside `CheckForNewModReleasesAsync` (lines 705–718) → `await _snapshotProvider(cancellationToken).ConfigureAwait(false);` — the lambda moves into `MainViewModel`'s ctor wiring, verbatim, wrapped in its existing `InvokeOnDispatcherAsync` helper.
5. `QueueDatabaseInfoRefresh(updateCandidates, true)` → `_onUpdateCandidates(updateCandidates)`.
6. `_databaseService.TryFetchLatestReleaseVersionAsync(modId, ct)` → `_fetchLatestReleaseVersionAsync(modId, ct)`.
7. `InternetAccessManager.IsInternetAccessDisabled` reads stay as-is (static, service can reference it directly).

**`MainViewModel` keeps:**
- `public bool IsFastCheckInProgress` property + `_isFastCheckInProgress` backing field + its `UpdateModDetailsProgressVisibility()` hook (lines 305–311) — unchanged; the service drives it via `onInProgressChanged`.
- `public void FastCheck() => _updatePollingService.FastCheck();` (public — called from `OnViewSectionChanged`, line 619; must stay public per golden rule).
- Call-site rewires: ctor line 216 `ResetFastCheckTimer()` → `_updatePollingService.ResetTimer()`; lines 1280/1282 `StopFastCheckTimer()`/`ResetFastCheckTimer()` → `StopTimer()`/`ResetTimer()`; `Dispose()` disposes the service.
- Ctor wiring (after `_databaseService` and busy-tracker init):

```csharp
_updatePollingService = new ModUpdatePollingService(
    FastCheckInterval,
    () => _isAutoRefreshDisabled,
    ct => InvokeOnDispatcherAsync(
        () =>
        {
            var snapshot = new List<ModEntry>(_modEntriesBySourcePath.Count);
            foreach (var entry in _modEntriesBySourcePath.Values)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.ModId)) continue;
                snapshot.Add(entry);
            }
            return snapshot;
        },
        ct),
    (modId, ct) => _databaseService.TryFetchLatestReleaseVersionAsync(modId, ct),
    entries => QueueDatabaseInfoRefresh(entries, true),
    inProgress => IsFastCheckInProgress = inProgress);
```

(`FastCheckInterval` const either moves to the service with the value passed in, or stays — put the const in the service and pass `ModUpdatePollingService.DefaultInterval`; either way exactly one definition. Note `onInProgressChanged` fires on a background thread exactly as the old inline code set the property from `RunFastCheckAsync` — behavior unchanged.)

- [ ] **Step 1: Create the service**, move members with the adaptations above.
- [ ] **Step 2: Rewire `MainViewModel`**, then stale-reference grep:

```bash
grep -n '_fastCheckTimer\|_hasPendingFastCheck\|_isFastCheckRunning\|FastCheckInterval\|ResetFastCheckTimer\|StopFastCheckTimer\|OnFastCheckTimerElapsed\|RunFastCheckAsync\|CheckForNewModReleasesAsync\|IsDifferentVersion' VintageStoryModManager/ViewModels/MainViewModel.cs
```

Expected: only the pass-through `FastCheck()` body, ctor wiring, and `_updatePollingService` field/calls remain; zero hits for the field/method names themselves.

- [ ] **Step 3: Build + verify**

```bash
dotnet build ./ImprovedModMenu.sln --configuration Release
./verify-methods.sh RunFastCheckAsync CheckForNewModReleasesAsync IsDifferentVersion OnFastCheckTimerElapsed
```

Expected: all `[OK] ...: 1` (now in the service). `FastCheck` reports 2 (facade + service).

- [ ] **Step 4: Format check + commit** (same format command pattern as Task 2, files: `ModUpdatePollingService.cs`, `MainViewModel.cs`)

```bash
git commit -m "refactor: extract ModUpdatePollingService from MainViewModel (slice 13 part 2, area M)"
```

---

### Task 4: Tests

**Files:**
- Create: `VintageStoryModManager.Tests/UserReportsCoordinatorTests.cs`
- Create: `VintageStoryModManager.Tests/ModUpdatePollingServiceTests.cs`

**Interfaces — Consumes:** Task 2's coordinator (internal `StoreUserReportEtag`/`StoreLatestReleaseUserReportEtag`/`BuildVoteEtagKey`), Task 3's service (public API + internal `IsDifferentVersion`). `InternalsVisibleTo` already grants access. Follow the existing test style (see `BusyStateTrackerTests.cs`, `ModlistCollectionsViewModelTests.cs`).

**UserReportsCoordinatorTests (target ~6–8):**
1. `BuildVoteEtagKey` composes prefix/modId/version/gameVersion deterministically and case-stably (assert two calls with different casing of modId produce keys that the case-insensitive dictionary treats as equal — mirror whatever the implementation actually does; read it first).
2. `SetVisibility(false)` then `SetVisibility(false)` → first returns true, second false.
3. `SetVisibility(false)` resets fetch-enabled state; `SetVisibility(true)` returns true but does not itself re-enable fetching.
4. Etag round-trip: `StoreUserReportEtag` + `StoreLatestReleaseUserReportEtag` → dispose → construct a second coordinator on the same cache path → keys load back (observable via storing null to delete and checking the persisted JSON, or expose the check the cheapest way the implementation allows — read `PersistVoteEtagsLocked`'s JSON shape first and assert on the file content).
5. Corrupt/missing `voteEtags.json` → constructor does not throw.
6. `QueueUserReportRefresh(null)` and queueing while `SetVisibility(false)` → no-ops (no throw, no busy-scope invocation — assert the injected `beginBusyScope` delegate was never called).

Constructor delegates for tests: `() => new StubDisposable()`, `() => "1.20.0"`, `() => Array.Empty<ModListItemViewModel>()` twice. Cache path: a temp directory file per test (`Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())`), deleted in cleanup.

**ModUpdatePollingServiceTests (target ~4–6):**
1. `IsDifferentVersion`: equal versions → false; different → true; null/whitespace latest → false; normalization cases (e.g. `"v1.2.0"` vs `"1.2.0"` — read the implementation's use of `VersionStringUtility.Normalize` first and test its actual contract).
2. `FastCheck()` with `isAutoRefreshDisabled: () => true` → snapshot provider never invoked.
3. `FastCheck()` with an empty snapshot → completes, `onUpdateCandidates` never invoked, `onInProgressChanged` saw `true` then `false` (poll with a short `Task.Delay` loop / `SemaphoreSlim` signal from the stub — no arbitrary long sleeps).
4. Snapshot with one entry whose fetched latest version differs from installed and from `DatabaseInfo` → `onUpdateCandidates` receives that entry.
5. Two entries sharing a modId → fetch delegate invoked once for that id (dedup preserved).

- [ ] **Step 1: Write UserReportsCoordinatorTests**, run: `dotnet test VintageStoryModManager.Tests --filter UserReportsCoordinatorTests` → all pass.
- [ ] **Step 2: Write ModUpdatePollingServiceTests**, run: `dotnet test VintageStoryModManager.Tests --filter ModUpdatePollingServiceTests` → all pass.
- [ ] **Step 3: Full suite:** `dotnet test ./ImprovedModMenu.sln --configuration Release` → all pass. (Known flake: WPF `*_wpftmp` CS0103 errors from stale Debug obj — re-run once, then `dotnet clean ... --configuration Debug` if it persists; not a code failure if Release builds clean.)
- [ ] **Step 4: Commit**

```bash
git add VintageStoryModManager.Tests/UserReportsCoordinatorTests.cs VintageStoryModManager.Tests/ModUpdatePollingServiceTests.cs
git commit -m "test: cover UserReportsCoordinator and ModUpdatePollingService (slice 13 part 3, area M)"
```

---

### Task 5: Final gates + report

- [ ] `dotnet build ./ImprovedModMenu.sln --configuration Release` → zero warnings, zero errors.
- [ ] Solution-wide unused-using check: `dotnet format ./ImprovedModMenu.sln style --diagnostics IDE0005 --severity info --verify-no-changes` → clean.
- [ ] Golden-rule final grep (public surface intact):

```bash
grep -rn '_viewModel\.\(EnableUserReportFetching\|SubmitUserReportVoteAsync\|RefreshUserReportAsync\|FastCheck\)\|UserReportVoteSubmitted\|ModUserReportChangedEventArgs' VintageStoryModManager/Views/ --include='*.cs'
```

All hits must be unmodified files (`git diff --name-only` must not include any `Views/` file — this slice touches no view code).

- [ ] Report back with: commit hashes + branch name, `git rev-parse` of your worktree root, the full **Adaptations** list (every non-verbatim change, including the ones this plan specified), line-count delta of `MainViewModel.cs`, and test count/results. Do **not** push, do not touch `CLAUDE.md` or stage `.claude/`.
