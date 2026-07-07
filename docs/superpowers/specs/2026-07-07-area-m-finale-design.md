# Area-M Finale Design: Enrichment Pipeline Service + Mod-Entry Diff Helper (Slice 21)

**Created:** 2026-07-07 (brainstormed and approved in-session)
**Status:** Approved by user 2026-07-07
**Source data:** `reports/mainviewmodel-responsibility/` (clusters 8 and 11, hot-fields.md), re-grounded against `MainViewModel.cs` at 2,893 lines (post-slice-17 tip)

## Goal

Finish area M's planned decomposition: move the mod-database-info **fetch pipeline** (cluster 11's orchestration half, ~500 lines) out of `MainViewModel` into a `ModDatabaseInfoRefreshService`, and extract the **pure entry-diff rules** from the Mod Loading core (cluster 8) into a static helper. After this slice, `MainViewModel` (~2,000 lines) contains only presentation-shaped code: bound properties, commands, the observable collections, ViewModel-item creation, selection preservation, and dispatcher glue.

## The MVVM line (why this cut and not a repository)

MVVM's division: **services own I/O and policy; the ViewModel owns presentation state.** Creating and maintaining `ModListItemViewModel`s in observable collections is view-model work — a service that manufactures ViewModels would be a layering smell. What doesn't belong in the VM is the fetch pipeline: timers, CTS, batching queues, refresh policy, HTTP/zip/JSON work. The zip/JSON half already left in slice 17 (`OfflineModDatabaseInfoBuilder`); this slice moves the orchestration that drives it.

A full `InstalledModsRepository` (service owns the `ModEntry` dictionary, VM subscribes to change events) was considered and rejected: big-bang risk, a second diffing layer in the VM to mirror changes into collections, and no consumer other than this one VM. YAGNI.

## The boundary decision (`_modEntriesBySourcePath` shared-write hazard, hot-fields.md)

Resolved by construction, not by locking:

- **The service never touches `_modEntriesBySourcePath` and never writes any `ModEntry` member.**
- **Input:** entries arrive as method arguments. This is already the code's shape — every `QueueDatabaseInfoRefresh` call site snapshots `_modEntriesBySourcePath.Values` in the VM before calling.
- **Output:** one apply-callback delivering `(entry, preparedInfo, loadLogoImmediately)` items, batched or immediate per the existing policy. The VM's callback implementation is the moved body of today's `ApplyDatabaseInfoBatchAsync`/`ApplyDatabaseInfoImmediateAsync`: it mutates `entry.DatabaseInfo`, updates the paired `ModListItemViewModel`, and marshals to the dispatcher.
- Result: **`MainViewModel` remains the dictionary's and the entries' single writer.** The enforcement is mechanical — the implementation plan carries a grep gate asserting no `entry.<member> =` assignment exists in the service file.

## Part A: `ModDatabaseInfoRefreshService`

**Moves (verbatim bodies + specified delegate substitutions), from cluster 11's orchestration half:**

- Fields: `_databaseRefreshLock`, `_databaseRefreshCts`, `_databaseRefreshTask`, `_databaseInfoBatchLock`, `_pendingDatabaseInfoUpdates`, `_databaseInfoBatchTimer`, `_suppressedTagEntries`.
- Methods: `QueueDatabaseInfoRefresh` (filter + cancel-and-restart), `RefreshDatabaseInfoBatchAsync`, `RefreshDatabaseInfoProgressivelyAsync`, `RefreshDatabaseInfoAsync` (per-entry decision flow), `PopulateOfflineDatabaseInfoAsync`, `PopulateOfflineInfoForEntryAsync`, `ApplyDatabaseInfoAsync` (routing head only — the batched/immediate *application* bodies stay in the VM as the callback), `ShouldUseBatchedUpdates`, `QueueDatabaseInfoUpdate`, `FlushDatabaseInfoBatch{,Locked}`, `NeedsDatabaseRefresh`, `ShouldSkipOnlineDatabaseRefresh`, `PrepareDatabaseInfoForVisibility`, `TryGetTagSuppressionKey`. (`_suppressedTagEntries`' writer and readers move together.)

**Injected dependencies:**

- `ModDatabaseService` (online fetch) and `OfflineModDatabaseInfoBuilder` (slice-17 service) — real services, constructor-injected.
- Gate delegates (VM-owned mutable state, read-only to the service): `Func<bool> isInitialLoad`, `Func<bool> isTagsColumnVisible`.
- Progress delegates: `onRefreshEnqueued(int, string?)` / `onRefreshCompleted(int)` → the VM's slice-15 `ModDetailsProgressTracker` pass-throughs.
- Apply callback: the single output channel described in the boundary section.
- `InternetAccessManager` statics stay direct reads (same as `ModUpdatePollingService`).

**Lifecycle:** `IDisposable`. `Dispose` replicates today's teardown verbatim: cancel + dispose the refresh CTS, wait up to 2 seconds on the in-flight task, dispose the batch timer, clear the pending list. `MainViewModel.Dispose` swaps its inline teardown for `_databaseInfoRefreshService.Dispose()`.

**Error handling:** unchanged, moved verbatim — per-entry catch blocks live inside the moved bodies; exceptions in the apply path surface on the VM side exactly as today.

## Part B: `ModEntryDiffHelper` (scout-first)

`LoadChangedModEntries`, `ResetCalculatedModState`, `CopyTransientModState` — the entry-diff and transient-state-preservation rules the audit map describes as pure — move to a static, internal-testable helper. **Scout-first with standing permission to abandon Part B** if grep shows them less pure than mapped (e.g. hidden reads of VM mutable state). `LoadModsAsync`, `PerformFullReloadAsync`, `ApplyPartialUpdates`, `CreateModViewModel`, selection preservation, and the counters stay in the VM — presentation orchestration.

## Golden rules (same as every area-M slice)

- `MainViewModel`'s public surface does not change. (`RefreshInstalledModDetails` is `internal` — verify its callers and preserve.)
- No `Views/` or `.xaml` changes.
- Move, don't rewrite; every non-verbatim change enumerated as an Adaptation.
- Release build 0/0; solution-wide IDE0005 clean (post-slice-20 the unscoped check is the standard gate).

## Testing

**Service (~10 tests, fakes = recorded delegates + stub fetch functions):**
1. Queue with `forceRefresh: false` filters via `NeedsDatabaseRefresh`; `forceRefresh: true` bypasses it.
2. Tag-suppression: suppressed entry + tags column visible → skipped without force; force clears the path.
3. Re-queue cancels the prior run (first run's token observed cancelled; no double apply).
4. Initial-load routing → batched apply (single flush delivers the batch); post-initial small set → immediate apply.
5. Offline path: fetch delegate returns null/throws → offline populate via a stubbed builder path → apply callback still receives merged info.
6. `PrepareDatabaseInfoForVisibility` strips tags when column hidden and records suppression.
7. Progress delegates: enqueued once per queued batch, completed per entry including on failure paths.
8. Dispose mid-run: no apply callbacks after dispose, no unobserved exceptions.
9. No-entries queue → no task started, no progress calls.
10. Batch timer flush delivers pending updates without an explicit flush call.

**Diff helper (~4 tests):** changed/added/removed entry detection; transient state (calculated fields) copied on unchanged entries; reset on changed entries; ordering stability.

Full existing suite (264 at design time) stays green. Smoke test (post-merge): initial load shows details progress + tags streaming in; toggle tags column off/on → suppression respected then refreshed; force refresh via Refresh command mid-load → prior run cancels cleanly, counts stay sane; offline mode → cached data still appears (slice-17 path through the new orchestration); dispose-on-exit mid-refresh → no crash.

## Out of scope

Clusters 3 (search), 4 (tag chips), 5 (sorting), 7 (status), 13 (selection/activation), 14 (presets), 16 (internet glue), 17 (misc) — presentation-shaped, they stay. Whether any deserve later trimming is a post-finale audit question. After this slice, area M's planned decomposition is complete; the remaining MVVM-phase work is the roadmap's per-area follow-ups, not MainViewModel.
