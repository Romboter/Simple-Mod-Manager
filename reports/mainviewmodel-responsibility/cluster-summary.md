# MainViewModel Responsibility Clusters

`VintageStoryModManager/ViewModels/MainViewModel.cs` — 4,749 lines, sealed `MainViewModel : ObservableObject, IDisposable`.
17 clusters identified. Every member in `member-inventory.csv` lands in exactly one cluster.

---

## 1. Construction & Disposal

**Purpose:** Wires up all services/collections in the constructor and tears them down in `Dispose`. Pure plumbing, not real "logic".

- **Size:** ~70 lines (ctor 159-217, `Dispose` 490-554, `DataDirectory` property).
- **Members:** `MainViewModel()` ctor, `DataDirectory`, `Dispose`.
- **Owned fields:** none exclusively (it initializes almost every field in the class).
- **Shared fields:** touches nearly all timers/CTS/collections during dispose.
- **Verdict:** (b) UI-notification/lifecycle glue that stays with whatever becomes the root VM — but it is also the single best map of every field owned by every other cluster, so it's a useful reference when splitting.

## 2. View Section / Tab Navigation

**Purpose:** Tracks which of the three tabs (Installed/Database/Modlist) is active and exposes commands + booleans the view binds to for tab visibility.

- **Size:** ~140 lines.
- **Members:** `CurrentModsView`, `ShowMainTabCommand`/`ShowDatabaseTabCommand`/`ShowModlistTabCommand`, `IsViewingModlistTab`, `IsViewingMainTab`, `SearchModDatabase`, `SetViewSection`, `ViewSection` enum.
- **Owned fields:** `_viewSection`, `_showMainTabCommand`, `_showDatabaseTabCommand`, `_showModlistTabCommand`.
- **Shared fields:** `_searchText`/`SearchText` (clears search on tab switch), `SelectedMod` (clears on tab switch), `InternetAccessManager.IsInternetAccessDisabled` (static, gates Database/Modlist tabs).
- **Existing delegations:** `InternetAccessManager` (static gate check).
- **Verdict:** (a) Good candidate for a small `TabNavigationViewModel`/service — self-contained enum + 4 fields, only reaches out to Search and SelectedMod at tab-switch boundaries.

## 3. Search & Filtering (installed mods text search)

**Purpose:** Owns the installed-mods search box: text, tokenization, adaptive debounce timer, and the `ModsView.Filter` predicate (which also calls into Tag Filtering).

- **Size:** ~180 lines.
- **Members:** `SearchText`, `HasSearchText`, `ClearSearchCommand`, `FilterMod`, `CreateSearchTokens`, `ClearSearchResults`, `CalculateAdaptiveSearchDebounce`, `TriggerDebouncedInstalledModsSearch`, `OnSearchDebounceTimerElapsed`, `ExecuteInstalledModsSearch`, `RefreshModsViewIfNotCancelled`.
- **Owned fields:** `_searchText`, `_searchTokens`, `_clearSearchCommand`, `_searchDebounceTimer`, `_pendingSearchCts`, `_searchDebounceLock`.
- **Shared fields:** `_mods` (read-only, for adaptive debounce sizing), `_tagFilterService` (via `FilterMod`), `ModsView`, `_searchResults`/`SelectedMod` (via `ClearSearchResults`), `_disposed`.
- **Verdict:** (a) Reasonably separable — the debounce/token machinery is self-contained; `FilterMod` is the one true coupling point to Tag Filtering (both predicates run inside the same `ICollectionView.Filter` delegate).

## 4. Tag Filtering

**Purpose:** Maintains the installed-mods tag-chip UI (`_installedTagFilters`), delegates the actual tag bookkeeping to `TagFilterService`, and reacts to per-mod `DatabaseTags` changes to keep the chip list in sync.

- **Size:** ~250 lines.
- **Members:** `InstalledTagFilters`, `HasSelectedTags`, `TagsColumnHeader`, `SetInstalledColumnVisibility`, `SetTagsColumnVisibility`, `SetUserReportsColumnVisibility`, `ScheduleInstalledTagFilterRefresh`, `ResetInstalledTagFilters`, `ApplyInstalledTagFilters`, `OnInstalledTagFilterPropertyChanged`, `OnModDatabaseTagFilterPropertyChanged` (dead), `SyncSelectedTagsToService`, `UpdateHasSelectedTags`.
- **Owned fields:** `_installedTagFilters`, `_lastInstalledAvailableTags`, `_isInstalledTagRefreshPending`, `_suppressInstalledTagFilterSelectionChanges`, `_hasSelectedTags`, `_isTagsColumnVisible`.
- **Shared fields:** `_mods`/`_searchResults` (clears tags when column hidden), `_allowModDetailsRefresh`, `_modEntriesBySourcePath` (triggers a DB refresh when tags column re-enabled), `_areUserReportsVisible`/`_hasEnabledUserReportFetching` (via the confusingly-adjacent `SetUserReportsColumnVisibility`, which is really a User Reports concern living here because it shares the "column visibility toggle" shape with `SetTagsColumnVisibility`).
- **Existing delegations:** `TagFilterService`, `TagCacheService` (`_tagCache` — actually barely used directly here; mostly behind `_tagFilterService`).
- **Note:** `ScheduleInstalledTagFilterRefresh` contains the file's only `async void` (a nested local function `ExecuteAsync`) — fire-and-forget, exceptions swallowed internally, so it's safe but worth flagging for anyone re-touching it.
- **Verdict:** (c) Tangled — `SetUserReportsColumnVisibility` doesn't belong here at all (it's User Reports state wearing a Tag Filtering trenchcoat because both column toggles were written side by side). Splitting this cluster requires first moving that one method out.

## 5. Sorting

**Purpose:** Defines the four built-in sort options and keeps the active sort re-applied when an "Active" sort is selected and activation state changes elsewhere.

- **Size:** ~65 lines.
- **Members:** `SortOptions`, `SelectedSortOption`, `CreateSortOptions`, `ReapplyActiveSortIfNeeded`, `IsActiveSortProperty`.
- **Owned fields:** `_sortOptions`, `_selectedSortOption`.
- **Shared fields:** `ModsView` (applies/refreshes it).
- **Verdict:** (a) Cleanly separable — smallest, most self-contained cluster in the file. Only touches `ModsView` from outside its own fields.

## 6. Busy State & Progress

**Purpose:** Two overlapping busy mechanisms: a generic ref-counted `BeginBusyScope`/`EndBusyScope` (with a debounced release so flicker-free), and a mod-details-specific progress counter (`_pendingModDetailsRefreshCount` / `_modDetailsRefreshTotalWork` / `_modDetailsRefreshCompletedWork`) used to drive a progress bar during database refreshes.

- **Size:** ~330 lines.
- **Members:** `IsBusy`, `EnterBusyScope`, `BeginBusyScope`, `EndBusyScope`, `ScheduleBusyRelease`, `UpdateIsBusy`, `RecalculateIsBusy`, `IsLoadingMods`, `LoadingProgress`, `LoadingStatusText`, `IsLoadingModDetails`, `IsModDetailsProgressVisible`, `ModDetailsProgress`, `ModDetailsStatusText`, `UpdateIsLoadingModDetails`, `UpdateModDetailsProgressVisibility`, `IsModDetailsRefreshPending`, `OnModDetailsRefreshEnqueued`, `OnModDetailsRefreshCompleted`, `EnsureModDetailsBusyScope`, `ReleaseModDetailsBusyScope`, `AddModDetailsWork`, `UpdateModDetailsProgress`, `ResetModDetailsProgress`, `BusyScope` nested class.
- **Owned fields:** `_busyStateLock`, `_busyOperationCount`, `_busyReleaseCts`, `_hasActiveBusyScope`, `_isBusy`, `_isLoadingMods`, `_loadingProgress`, `_loadingStatusText`, `_isLoadingModDetails`, `_isModDetailsProgressVisible`, `_modDetailsProgress`, `_modDetailsStatusText`, `_modDetailsBusyScopeLock`, `_modDetailsBusyScope`, `_pendingModDetailsRefreshCount`, `_modDetailsRefreshTotalWork`, `_modDetailsRefreshCompletedWork`, `_modDetailsProgressStage`.
- **Shared fields:** `_isFastCheckInProgress` (Fast Check cluster owns it, this cluster reads it in `UpdateModDetailsProgressVisibility`).
- **Existing delegations:** dispatcher marshalling (`Application.Current?.Dispatcher`) is hand-rolled in three near-identical methods (`UpdateIsBusy`, `RecalculateIsBusy`, `UpdateIsLoadingModDetails`) rather than reusing `InvokeOnDispatcherAsync`.
- **Verdict:** (a)/(b) mixed — the ref-counted busy-scope mechanism is a clean, self-contained candidate for a small `BusyStateTracker` service; the mod-details progress counter is UI-notification glue that's fine to leave, but it is called from Mod DB Info Enrichment (`OnModDetailsRefreshEnqueued`/`Completed`) constantly, so extracting one half without the other creates an awkward seam.

## 7. Status Reporting

**Purpose:** The single-line status bar text (`StatusMessage`/`IsErrorStatus`) plus the two message-builder helpers for the mod-details loading/ready text.

- **Size:** ~60 lines.
- **Members:** `StatusMessage`, `HasStatusMessage`, `IsErrorStatus`, `ReportStatus`, `SetStatus`, `UpdateLoadedModsStatus`, `BuildModDetailsLoadingStatusMessage`, `BuildModDetailsReadyStatusMessage`.
- **Owned fields:** `_statusMessage`, `_isErrorStatus`, `_isModDetailsStatusActive`, `_hasShownModDetailsLoadingStatus`.
- **Shared fields:** `TotalMods` (Mod Loading cluster), `_pendingModDetailsRefreshCount`-derived state (via `IsModDetailsRefreshPending`, Busy State cluster).
- **Existing delegations:** `StatusLogService.AppendStatus` (static, external log sink) — every status change is also mirrored there.
- **Verdict:** (a) Small and separable as a `IStatusReporter`-style facade (this is effectively what `MainWindow.StatusReporting.cs` already does on the View side per `CLAUDE.md`'s prior MainWindow work) — but it is called from almost every other cluster (7+ call sites), so it should be an injected dependency, not something that moves fields out from under callers.

## 8. Mod Loading & Refresh

**Purpose:** The mod-list backbone: full/incremental discovery via `ModDiscoveryService`, view-model creation, the `_mods`/`_modEntriesBySourcePath`/`_modViewModelsBySourcePath` triad, collection-changed wiring, and the client-settings/mods-watcher change-detection loop that decides whether a refresh is needed at all.

- **Size:** ~950 lines — the largest cluster by far.
- **Members:** `ModsView`, `SearchResultsView`, `ModsWatcher`, `TimingService`, `TotalMods`, `ActiveMods`, `UpdatableModsCount`, `SummaryText`, `UpdateAllButtonLabel`/`MenuHeader`, `NoModsFoundMessage`, `RefreshCommand`, `InitializeAsync`, `ForceNextRefreshToLoadDetails`, `GetSourcePathsForModsWithErrors`, `RefreshModsWithErrorsAsync`, `LoadModsAsync`, `PerformFullReloadAsync`, `LoadingProgressUpdate`, `CheckForModStateChangesAsync`, `ApplyClientSettingsChangesAsync`, `CreateModViewModel`, `UpdateModsStateSnapshotAsync`, `CaptureModsStateFingerprintAsync`, `UpdateActiveCount`, `UpdateUpdatableCount`, `OnModsCollectionChanged`, `OnSearchResultsCollectionChanged`, `EnumerateModItems`, `Attach/DetachInstalledMod(s)`, `Attach/DetachSearchResult(s)`, `On{Installed,SearchResult}ModPropertyChanged`, `LoadChangedModEntries`, `ResetCalculatedModState`, `CopyTransientModState`, `ApplyPartialUpdates`, `PerformClientSettingsCleanupIfNeeded`.
- **Owned fields:** `_modEntriesBySourcePath`, `_modViewModelsBySourcePath`, `_mods`, `_modsWatcher`, `_modsStateLock`, `_modsStateFingerprint`, `_clientSettingsWatcher`, `_totalMods`, `_activeMods`, `_updatableModsCount`, `_installedModSubscriptions`, `_searchResultSubscriptions`, `_isInitialLoad`, `_isModDetailsRefreshForced`, `_cachedBasePaths` (invalidated here, owned by Misc Infrastructure).
- **Shared fields:** `SelectedMod` (read/written constantly to preserve selection across reloads), `_allowModDetailsRefresh`/`_isAutoRefreshDisabled` (gates whether a reload also triggers Mod DB Info Enrichment), `_discoveryService`, `_settingsStore`, `_configuration`, `_timingService`.
- **Existing delegations:** `ModDiscoveryService` (actual filesystem scan), `ModDirectoryWatcher`, `ClientSettingsWatcher`, `ClientSettingsStore`.
- **Verdict:** (c) Tangled and oversized — this is the true core of the class and the hardest to extract cleanly. It owns the mod collections that almost every other cluster reads (`_mods`, `_modEntriesBySourcePath`), and it directly triggers Mod DB Info Enrichment (`QueueDatabaseInfoRefresh`) and User Reports (`QueueUserReportRefresh` via attach) as side effects of loading. Any split needs this to become the "mod repository" service that other pieces depend on, not the other way around.

## 9. Fast Check & Update Polling

**Purpose:** A background polling loop (every `FastCheckInterval` = 2 min, or on-demand via `FastCheck()`) that checks installed mods for newer releases and refreshes user-report vote summaries for currently-displayed mods, throttled by a re-entrancy flag.

- **Size:** ~330 lines.
- **Members:** `IsFastCheckInProgress`, `FastCheck`, `RunFastCheckAsync`, `ResetFastCheckTimer`, `StopFastCheckTimer`, `OnFastCheckTimerElapsed`, `CheckForNewModReleasesAsync`, `CheckForVoteChangesAsync`, `CheckVoteChangesForModAsync`, `IsDifferentVersion`, `VoteCheckTarget` record.
- **Owned fields:** `_fastCheckTimer`, `_fastCheckTimerLock`, `_hasPendingFastCheck`, `_isFastCheckRunning`, `_isFastCheckInProgress`.
- **Shared fields:** `_modEntriesBySourcePath`/`_mods` (Mod Loading cluster, read-only snapshot each tick), `InstalledGameVersion`, `_userReportEtags`/`_latestReleaseUserReportEtags` (User Reports cluster's etag cache, read+write via `StoreUserReportEtag`/`StoreLatestReleaseUserReportEtag`), `_isAutoRefreshDisabled`/`InternetAccessManager.IsInternetAccessDisabled` (gates).
- **Existing delegations:** `_databaseService.TryFetchLatestReleaseVersionAsync`, `_voteService.GetVoteSummaryIfChangedAsync`, `VersionStringUtility.Normalize`.
- **Verdict:** (a) Good candidate for a `ModUpdatePollingService` — the timer/re-entrancy machinery is self-contained; its only real coupling is reading the mod snapshot (Mod Loading) and pushing through the etag cache (User Reports), both of which are read/write through narrow existing methods, not raw field access.

## 10. User Reports & Voting

**Purpose:** Everything to do with the community "user report" (vote) feature: fetching/caching vote summaries per mod+version with ETag-based conditional requests, submitting/removing votes, and the operation-count bookkeeping used to drive busy state during report fetches.

- **Size:** ~530 lines.
- **Members:** `UserReportVoteSubmitted` event, `StoreUserReportEtag`, `StoreLatestReleaseUserReportEtag`, `GetVoteEtag`, `UpdateVoteEtag`, `BuildVoteEtagKey`, `LoadVoteEtagsFromDisk`, `PersistVoteEtagsLocked`, `VoteEtagCacheState`, `RunUserReportOperationAsync`, `Begin/EndUserReportOperation`, `Queue{,LatestRelease}UserReportRefresh`, `RefreshLatestReleaseUserReportAsync`/`CoreAsync`, `EnableUserReportFetching`, `RefreshUserReportAsync`, `SubmitUserReportVoteAsync`, `RaiseUserReportVoteSubmitted`, `RefreshUserReportCoreAsync`, `ModUserReportChangedEventArgs`, `UserReportOperationScope`.
- **Owned fields:** `_userReportEtags`, `_latestReleaseUserReportEtags`, `_voteEtagCachePath`, `_voteEtagPersistenceLock`, `_userReportOperationLock`, `_activeUserReportOperations`, `_userReportRefreshLimiter`, `_voteService`, `_areUserReportsVisible`, `_hasEnabledUserReportFetching`, `_hasFetchedUserReportsThisSession`.
- **Shared fields:** `InstalledGameVersion` (read everywhere), `_installedModSubscriptions`/`_searchResultSubscriptions` (Mod Loading cluster's subscription sets, iterated in `EnableUserReportFetching`), busy-scope (via `BeginBusyScope`/`RunUserReportOperationAsync`).
- **Existing delegations:** `ModVersionVoteService` (`_voteService`) does the actual HTTP calls; this cluster is entirely orchestration + caching around it.
- **Verdict:** (a) Strong candidate for extraction into its own `UserReportsViewModel`/service — well-bounded around `ModVersionVoteService`, and its only external reads are the mod subscription sets and `InstalledGameVersion`. The awkward part is `SetUserReportsColumnVisibility` currently lives in the Tag Filtering cluster instead of here (see cluster 4's verdict) — that should move first.

## 11. Mod Database Info Enrichment

**Purpose:** The largest single mechanism in the file: decides which installed mods need an online/offline "database info" refresh (tags, downloads, releases, logo), fetches it (online via `ModDatabaseService`, or synthesizes it offline from cached release archives), batches UI updates, and merges offline+cached release lists.

- **Size:** ~1,200 lines — larger than Mod Loading & Refresh in member count, almost entirely `private static` helpers.
- **Members:** `RefreshInstalledModDetails`, `QueueDatabaseInfoRefresh`, `RefreshDatabaseInfoBatchAsync`, `NeedsDatabaseRefresh`, `ShouldSkipOnlineDatabaseRefresh`, `TryGetTagSuppressionKey`, `RefreshDatabaseInfoProgressivelyAsync`, `RefreshDatabaseInfoAsync`, `Populate{Offline,OfflineInfoForEntry}Async`, `ApplyDatabaseInfoAsync`, `ShouldUseBatchedUpdates`, `QueueDatabaseInfoUpdate`, `FlushDatabaseInfoBatch{,Locked}`, `ApplyDatabaseInfoBatchAsync`, `ApplyDatabaseInfoImmediateAsync`, `PrepareDatabaseInfoForVisibility`, `CreateInfoWithoutTags`, `CreateOfflineDatabaseInfo`, `MergeOfflineAndCachedInfo`, `MergeReleases`, `CreateOfflineRelease(s)`, `EnumerateCachedModReleases`, `TryCreateCachedRelease`, `IsModIdMatch`, `FindArchiveEntry`, `GetString`, `TryGetProperty`, `TryResolveVersionFromMap`, `ParseDependencies`, `AggregateRequiredGameVersions`, `DetermineOfflineLastUpdatedUtc`, `CompareOfflineReleases`, `ExtractRequiredGameVersions`, `DetermineInstalledGameCompatibility`, `TryCreateFileUri`, `TryGetReleaseFileName`, `TryGetLastWriteTimeUtc`.
- **Owned fields:** `_databaseRefreshLock`, `_databaseRefreshCts`, `_databaseRefreshTask`, `_databaseInfoBatchLock`, `_pendingDatabaseInfoUpdates`, `_databaseInfoBatchTimer`, `_suppressedTagEntries`.
- **Shared fields:** `_modEntriesBySourcePath` (Mod Loading cluster — read+write, the biggest coupling point), `_allowModDetailsRefresh`, `_isInitialLoad`, `_isTagsColumnVisible` (Tag Filtering), `_configuration.RequireExactVsVersionMatch`, `InstalledGameVersion`, calls into Busy State (`OnModDetailsRefreshEnqueued`/`Completed`) on essentially every entry point.
- **Existing delegations:** `ModDatabaseService` (online fetch), raw `ZipArchive`/`JsonElement` parsing of cached mod release files (this is doing filesystem+zip+JSON parsing work that arguably belongs in a service, not the view model).
- **Verdict:** (c) Tangled by size and by directly manipulating `_modEntriesBySourcePath`, but the *content* is unusually self-contained logic (zip/JSON parsing, static helpers) that has almost no WPF/dispatcher dependency of its own — a good target for a dedicated `ModDatabaseInfoService`, provided the caller passes in the mod-entry dictionary rather than the enrichment code reaching into it directly.

## 12. Mod Database Search / Browse — **dead code**

**Purpose (as written):** Looked like the pipeline for converting `ModDatabaseSearchResult`s into `ModListItemViewModel`s for a database-search results list (`_searchResults`/`SearchResultsView`).

- **Size:** ~170 lines.
- **Members:** `UpdateSearchResultsAsync`, `LoadModDatabaseLogosAsync`, `GetInstalledModIdsAsync`, `IsResultInstalled`, `CreateSearchResultEntry`, `CreateSearchResultViewModel`, `BuildSearchResultDescription`, `BuildModDatabasePageUrl`, `RejectActivationChangeAsync`.
- **Verified dead:** `grep -rn "UpdateSearchResultsAsync\|LoadModDatabaseLogosAsync\|GetInstalledModIdsAsync\|CreateSearchResultEntry\|CreateSearchResultViewModel\|IsResultInstalled" VintageStoryModManager/` returns **only their own declarations** in `MainViewModel.cs` — no callers anywhere in the repo (checked both the ViewModel file itself and every `.cs` file under `VintageStoryModManager/`).
- **Why it's dead:** The actual mod-database browse/install flow now lives in `MainWindow.ModBrowserInstallation.cs`, which has its own `ConvertToModListItemViewModel(DownloadableMod mod)` (line 44) built on `DownloadableModConverter.ToModEntry` — a completely separate, newer conversion path. This cluster appears to be a superseded first implementation that was never deleted.
- **Verdict:** Not (a)/(b)/(c) — flag for deletion in a future cleanup pass (out of scope here; this is a read-only map).

## 13. Selected Mod / Activation & Snapshots

**Purpose:** Tracks the current grid selection and exposes read-only snapshot/query methods over `_mods` used by presets, modlist save/backup, and PDF export in `MainWindow`; also owns per-mod activation persistence (`ApplyActivationChangeAsync`) and the global "auto refresh disabled" toggle.

- **Size:** ~370 lines.
- **Members:** `SelectedMod`, `HasSelectedMod`, `HasSelectedMods`, `HasMultipleSelectedMods`, `SetSelectedMod`, `RemoveSearchResult`, `FindInstalledModById`, `FindModBySourcePath`, `PreserveActivationStateAsync`, `ApplyActivationChangeAsync`, `SetAutoRefreshDisabled`, `GetCurrentDisabledEntries`, `GetCurrentModStates`, `GetInstalledModsSnapshot`, `GetActiveModUsageSnapshot`, `GetActiveModIdsSnapshot`, `TryGetInstalledModDisplayName`.
- **Owned fields:** `_selectedMod`, `_hasSelectedMods`, `_hasMultipleSelectedMods`.
- **Shared fields:** `_mods` (read-only iteration in nearly every snapshot method), `_modViewModelsBySourcePath`, `_settingsStore` (activation persistence), `_isAutoRefreshDisabled`/`_allowModDetailsRefresh` (written by `SetAutoRefreshDisabled`, read by Mod Loading/Enrichment/Fast Check).
- **Verdict:** (c) — the snapshot/query methods (`GetCurrentModStates`, `GetInstalledModsSnapshot`, etc.) are read-only projections over `_mods` and would extract cleanly as a facade; but `SetAutoRefreshDisabled` and `ApplyActivationChangeAsync` reach into cross-cutting flags (`_isAutoRefreshDisabled`, `_allowModDetailsRefresh`) that Mod Loading, Fast Check, and Enrichment all depend on — those two methods are really "global refresh policy" and don't belong with the read-only snapshot methods.

## 14. Preset Application

**Purpose:** Applies a `ModPreset`'s activation states (or disabled-entries list) to installed mods.

- **Size:** ~120 lines (`ApplyPresetAsync`, one method).
- **Owned fields:** none.
- **Shared fields:** `_mods`, `_settingsStore`, `SelectedSortOption`/`ModsView` (re-applies sort/refresh after bulk activation change), `UpdateActiveCount` (Mod Loading), `SetStatus` (Status Reporting).
- **Verdict:** (b) — small, does one job, mostly calls out to already-owned cross-cutting helpers (`UpdateActiveCount`, `SetStatus`, `_settingsStore.TrySetActive`). Not worth its own service; natural home is next to `ApplyActivationChangeAsync` in cluster 13.

## 15. Cloud/Local Modlist Collections

**Purpose:** Thin `ObservableCollection` wrappers exposing cloud and local modlist entries to the UI (populated externally by `MainWindow`'s cloud/local-modlist workflows, not by this view model).

- **Size:** ~55 lines.
- **Members:** `CloudModlistsView`, `LocalModlistsView`, `HasCloudModlists`, `HasLocalModlists`, `ReplaceCloudModlists`, `TryReplaceCloudModlist`, `ReplaceLocalModlists`.
- **Owned fields:** `_cloudModlists`, `_localModlists`.
- **Shared fields:** none of substance — fully self-contained.
- **Verdict:** (a) Trivially separable — could be its own tiny `ModlistCollectionsViewModel`/pair of observable collections injected into `MainViewModel`, with zero coupling to anything else in the class.

## 16. Internet Access State

**Purpose:** Reacts to the global `InternetAccessManager.InternetAccessChanged` static event to refresh command CanExecute state and bounce the user off the Database/Modlist tabs if internet access gets disabled while viewing them.

- **Size:** ~55 lines.
- **Members:** `CanAccessCloudModlists`, `OnInternetAccessStateChanged`, `OnInternetAccessChanged`, `RefreshInternetAccessDependentState`.
- **Owned fields:** none exclusively.
- **Shared fields:** `_mods`/`_searchResults` (calls `RefreshInternetAccessDependentState` per-mod), `_viewSection` (View Section cluster, force-switches tabs), `_allowModDetailsRefresh`/`_modEntriesBySourcePath` (triggers a DB refresh via `OnInternetAccessStateChanged`), `_showDatabaseTabCommand`/`_showModlistTabCommand` CanExecute re-eval.
- **Existing delegations:** `InternetAccessManager` static class (the actual on/off state and event).
- **Verdict:** (b) UI-notification glue — small, but it pokes at four other clusters' state (View Section, Mod Loading, User Reports via per-mod `SetUserReportOffline`/`QueueUserReportRefresh`, Enrichment), so it should stay as an event-handler adapter rather than become its own service.

## 17. Misc Infrastructure

**Purpose:** Grab-bag of small, low-risk helpers that don't fit anywhere else: player identity passthroughs, display-path shortening, static dispatcher-marshalling helpers, and a couple of plain UI-preference booleans.

- **Size:** ~140 lines.
- **Members:** `PlayerUid`, `PlayerName`, `IsCompactView`, `IsModInfoExpanded`, `UseModDbDesignView`, `InstalledGameVersion`, `GetDisplayPath`, `GetBasePathsList`, `EnumerateBasePaths` (dead — see below), `InvokeOnDispatcherAsync` (both overloads).
- **Owned fields:** `_isCompactView`, `_isModInfoExpanded`, `_useModDbDesignView`, `_cachedBasePaths`.
- **Shared fields:** `_settingsStore` (path base candidates, player identity).
- **Dead code:** `EnumerateBasePaths()` (line 3170) has no callers anywhere in the repo — `GetDisplayPath` calls `GetBasePathsList()` directly instead (`_cachedBasePaths ??= GetBasePathsList()`, line 3126), leaving this wrapper orphaned. Verified via `grep -rn "EnumerateBasePaths" VintageStoryModManager/` → only its own declaration.
- **Verdict:** (a)/(b) mixed — `InvokeOnDispatcherAsync` is genuinely reusable infrastructure (used by ~15 other members across clusters) and should probably become a static utility class rather than live on the VM; the rest is trivial passthroughs that can stay wherever.

---

## Cross-cluster call summary (selected, non-exhaustive)

- Mod Loading & Refresh → Mod Database Info Enrichment: `QueueDatabaseInfoRefresh` called from `LoadModsAsync`, `PerformFullReloadAsync`, `RefreshModsWithErrorsAsync`, `OnInternetAccessStateChanged`, `RefreshInstalledModDetails`, `SetTagsColumnVisibility`.
- Mod Loading & Refresh → User Reports & Voting: `AttachInstalledMod`/`AttachSearchResult` call `QueueUserReportRefresh`/`QueueLatestReleaseUserReportRefresh`.
- Mod Database Info Enrichment / User Reports / Search & Filtering / Preset Application / Selected Mod cluster → Status Reporting: all call `SetStatus`/`ReportStatus`.
- Mod Database Info Enrichment / User Reports / Fast Check → Busy State & Progress: all wrap work in `BeginBusyScope()`/`OnModDetailsRefreshEnqueued`/`Completed`.
- Tag Filtering → Search & Filtering: `FilterMod` calls `_tagFilterService.PassesInstalledTagFilter` before checking `_searchTokens`.
- Internet Access State → View Section, Mod Loading, User Reports, Mod DB Info Enrichment (see cluster 16).
