# Split Candidates (ranked)

This is a map of where a future split is *least* costly, not an implementation plan. Ranked by
(separability) minus (blast radius of what it touches). See `cluster-summary.md` and `hot-fields.md`
for the underlying evidence.

## 0. Delete dead code first (not a split, but should happen before any split)

**Cluster 12 (Mod Database Search/Browse) is entirely dead** — `UpdateSearchResultsAsync`,
`LoadModDatabaseLogosAsync`, `GetInstalledModIdsAsync`, `IsResultInstalled`, `CreateSearchResultEntry`,
`CreateSearchResultViewModel`, `BuildSearchResultDescription`, `BuildModDatabasePageUrl`,
`RejectActivationChangeAsync` (~170 lines) have zero callers anywhere in the repo — verified with
`grep -rn` for each name across `VintageStoryModManager/`. `EnumerateBasePaths` (Misc Infrastructure,
line 3170) and `OnModDatabaseTagFilterPropertyChanged` (Tag Filtering, line 2621, empty body) are
likewise dead. Removing these ~200 lines first shrinks the file and removes noise before anyone tries
to reason about which clusters are worth extracting. **Risk: near zero** — confirmed no call sites, but
still worth a real build+smoke-test pass in case of reflection/binding surprises (none expected; these
are plain private/static methods).

## 1. Cloud/Local Modlist Collections → own view-model/service

**What it'd look like:** Extract `_cloudModlists`/`_localModlists`, their `ICollectionView`s, and
`ReplaceCloudModlists`/`TryReplaceCloudModlist`/`ReplaceLocalModlists`/`HasCloudModlists`/`HasLocalModlists`
into a small `ModlistCollectionsViewModel` that `MainViewModel` either composes or exposes via a
sub-property.

**What blocks it:** Nothing structural — this cluster shares no fields with the rest of the class
(see cluster-summary.md #15). The only friction is `INotifyPropertyChanged` surface: `HasCloudModlists`/
`HasLocalModlists` are read directly off `MainViewModel` by XAML bindings today, so extraction means
either re-exposing them as pass-through properties or a binding-path change in the views.

**Risk: low.** Smallest, most self-contained cluster with real behavior (as opposed to cluster 5/15
which are also small but Sorting still touches `ModsView`).

## 2. Fast Check & Update Polling → `ModUpdatePollingService`

**What it'd look like:** Extract the timer/re-entrancy machinery (`FastCheck`, `RunFastCheckAsync`,
`ResetFastCheckTimer`, `StopFastCheckTimer`, `OnFastCheckTimerElapsed`, `CheckForNewModReleasesAsync`,
`CheckForVoteChangesAsync`/`CheckVoteChangesForModAsync`) into a service that's handed a read-only
snapshot provider (a `Func<IReadOnlyList<ModEntry>>` or similar) instead of reaching into
`_modEntriesBySourcePath`/`_mods` directly, plus a callback for "here are mods that need a DB refresh"
instead of calling `QueueDatabaseInfoRefresh` directly.

**What blocks it:**
- Needs a snapshot accessor for `_mods`/`_modEntriesBySourcePath` (Mod Loading's hot fields) — doable
  via delegate injection, not a hard blocker.
- Directly writes into the etag caches (`_userReportEtags`/`_latestReleaseUserReportEtags`) that
  User Reports & Voting owns — those would need to move with it, or Fast Check keeps calling back into
  a `UserReportsService.StoreEtag(...)` API instead of touching the dictionaries.
- `IsFastCheckInProgress` feeds `UpdateModDetailsProgressVisibility` in Busy State & Progress — a
  property-changed/event bridge would be needed.
- Dispatcher use: reads `_mods`/`_modEntriesBySourcePath` via `InvokeOnDispatcherAsync`, so the service
  needs the same dispatcher-marshalling helper (candidate for its own static utility, see cluster 17).

**Risk: medium.** Timer/re-entrancy logic itself is clean; the etag-cache coupling with User Reports
is the main thing to resolve first (arguably Fast Check and User Reports should extract *together*).

## 3. User Reports & Voting → `UserReportsViewModel`/service

**What it'd look like:** Extract the vote-fetch/submit/etag-cache machinery
(`RunUserReportOperationAsync`, `Begin/EndUserReportOperation`, `Queue*UserReportRefresh`,
`Refresh*UserReportAsync`/`CoreAsync`, `SubmitUserReportVoteAsync`, `RaiseUserReportVoteSubmitted`, the
etag dictionaries/persistence, `ModVersionVoteService`) into its own class, keeping the
`UserReportVoteSubmitted` event as its public surface.

**What blocks it:**
- `SetUserReportsColumnVisibility` currently lives in the Tag Filtering cluster (cluster 4) purely by
  accident of code proximity — it should move into this cluster's territory *before* extraction, or
  the extracted service won't own its own visibility toggle.
- Reads `_installedModSubscriptions`/`_searchResultSubscriptions` (Mod Loading's subscription sets) in
  `EnableUserReportFetching` — needs those passed in or exposed as an enumerable.
- Shares busy-scope with the rest of the class (`BeginBusyScope`) — either the service gets a callback
  into the shared busy tracker, or busy state has to extract at the same time (see candidate 5).
- Fast Check writes directly into this cluster's etag dictionaries (see candidate 2) — resolve together.

**Risk: medium.** Well-bounded around `ModVersionVoteService`, but has real coupling to Mod Loading
(subscription sets) and to Fast Check (etag writes) that needs an explicit API instead of shared fields.

## 4. View Section / Tab Navigation → small navigation object

**What it'd look like:** `_viewSection` + the three tab commands + `IsViewingModlistTab`/`IsViewingMainTab`/
`SearchModDatabase`/`CurrentModsView` become a `TabNavigationState` object that `MainViewModel` exposes.

**What blocks it:**
- `SetViewSection` reaches into `SearchText` (Search & Filtering) and `SelectedMod` (Selected Mod cluster)
  to clear them on tab switch — needs either an event ("tab changed") that those clusters subscribe to,
  or the extracted object keeps a couple of callback delegates.
- Internet Access State force-switches tabs directly (`SetViewSection(ViewSection.MainTab)`) when
  connectivity drops while on a gated tab — another cross-cluster call to convert to an event.
- The `Dispatcher.BeginInvoke(... FastCheck())` call inside `SetViewSection` ties navigation to Fast
  Check too (triggers a fast check when returning to the main tab).

**Risk: medium-low.** Small in size, but has more silent side-effect fan-out (3 other clusters) than
its line count suggests — worth doing as an event-based facade rather than a field move.

## 5. Busy State & Progress → `BusyStateTracker` (partial extraction)

**What it'd look like:** The ref-counted busy-scope half (`BeginBusyScope`/`EndBusyScope`/
`ScheduleBusyRelease`/`UpdateIsBusy`/`RecalculateIsBusy`/`IsBusy`/`BusyScope` class) is a clean,
self-contained state machine that could become a standalone `IBusyScopeTracker` used by every other
cluster via constructor injection.

**What blocks it:**
- The mod-details progress counter half (`OnModDetailsRefreshEnqueued`/`Completed`,
  `_pendingModDetailsRefreshCount`, etc.) is called by Mod Database Info Enrichment dozens of times and
  is conceptually coupled to it, not to the generic busy-scope mechanism — splitting the *whole*
  cluster in one move would just relocate the tangle, not resolve it. Recommend extracting only the
  ref-counted busy-scope half first.
- `IsLoadingMods`/`IsLoadingModDetails` feed `RecalculateIsBusy`, so the boolean-combination logic has
  to stay wherever `IsBusy` lives.

**Risk: medium.** Worth doing, but as two separate moves (busy-scope tracker now, mod-details progress
counter later once/if Mod DB Info Enrichment itself gets a service boundary).

## Not recommended to extract yet

- **Mod Loading & Refresh** (cluster 8) and **Mod Database Info Enrichment** (cluster 11) are the two
  largest clusters and the ones everything else depends on (`_mods`, `_modEntriesBySourcePath`). They
  are legitimate service-extraction targets long-term (a "mod repository" service and a "mod database
  info" service respectively) but should be done *last*, after the smaller/cleaner clusters above have
  been peeled off — extracting them first means every other extraction has to negotiate with a moving
  target.
- **Selected Mod / Activation & Snapshots** (cluster 13) is genuinely tangled (per cluster-summary.md's
  verdict) between read-only snapshot projections (easy) and global refresh-policy flags
  (`_isAutoRefreshDisabled`/`_allowModDetailsRefresh`, hard) — needs to be split into two pieces
  internally before either piece is worth extracting.
