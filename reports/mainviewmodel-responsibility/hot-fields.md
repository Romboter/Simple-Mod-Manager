# Hot Fields

Fields read or written by 3+ clusters (see `cluster-summary.md` for cluster definitions). These are the
coupling points that constrain any future split of `MainViewModel` — a field here can't move into a
single extracted service without either duplicating state or wiring an event/callback back to the rest
of the class.

## `_mods` — `BatchedObservableCollection<ModListItemViewModel>`

The installed-mods collection. The single hottest field in the file.

- **Owner:** Mod Loading & Refresh (populates/clears/replaces it).
- **Also touched by:**
  - Selected Mod / Activation & Snapshots — read-only iteration in every `Get*Snapshot`/`Find*` method.
  - Tag Filtering — `_tagFilterService.UpdateInstalledAvailableTagsFromMods(_mods)`, `mod.ClearDatabaseTags()` loop.
  - Preset Application — iterates to read/set activation state.
  - Internet Access State — `foreach (var mod in _mods) mod.RefreshInternetAccessDependentState()`.
  - Fast Check & Update Polling — `CheckForVoteChangesAsync` iterates for vote-refresh targets.
  - Search & Filtering — `CalculateAdaptiveSearchDebounce` reads `_mods.Count` only.
- **Read vs written:** written only by Mod Loading (`LoadModsAsync`, `PerformFullReloadAsync`, `ApplyPartialUpdates`); every other cluster only reads/iterates.

## `_modEntriesBySourcePath` — `Dictionary<string, ModEntry>`

The raw discovered-mod-entry index, keyed by source path.

- **Owner:** Mod Loading & Refresh.
- **Also touched by:**
  - Mod Database Info Enrichment — read for refresh candidates, **written** by `ApplyDatabaseInfoBatchAsync`/`ApplyDatabaseInfoImmediateAsync` (updates `entry.DatabaseInfo` in place).
  - Fast Check & Update Polling — `CheckForNewModReleasesAsync` snapshots it for update checks.
  - Internet Access State — `OnInternetAccessStateChanged` reads it to decide whether to queue a refresh.
- **Read vs written:** Mod Loading owns writes on load/reload; Mod DB Info Enrichment also writes (mutates entries and re-inserts), which is a real shared-write hazard if the two clusters were ever split into separate services running on different threads without a shared lock.

## `_modViewModelsBySourcePath` — `Dictionary<string, ModListItemViewModel>`

- **Owner:** Mod Loading & Refresh.
- **Also touched by:** Selected Mod / Activation & Snapshots (`FindModBySourcePath`, restoring `SelectedMod` after reload).
- Only 2 clusters — included here because it's tightly paired with `_modEntriesBySourcePath` (always kept in sync) and would need to travel with it in any split.

## `SelectedMod` (backed by `_selectedMod`) — `ModListItemViewModel?`

- **Owner:** Selected Mod / Activation & Snapshots.
- **Also touched by:**
  - View Section / Tab Navigation — cleared on every tab switch.
  - Search & Filtering — cleared in `ClearSearchResults`.
  - Mod Loading & Refresh — set/restored across `PerformFullReloadAsync`/`ApplyPartialUpdates` to preserve selection by source path.
- **Read vs written:** written from all four clusters; this is a genuine multi-writer property, not just multi-reader.

## `_searchResults` — `ObservableCollection<ModListItemViewModel>`

- **Owner:** nominally Mod Loading & Refresh (attach/detach subscription wiring lives there) but its actual population code (Mod Database Search / Browse cluster) is dead.
- **Also touched by:**
  - Selected Mod / Activation & Snapshots — `RemoveSearchResult`.
  - Search & Filtering — `ClearSearchResults`.
  - Internet Access State — per-item `RefreshInternetAccessDependentState`.
- Because the code that's supposed to populate it (`UpdateSearchResultsAsync`) is dead, this field is currently only ever cleared, never filled, by anything reachable from the rest of the app (see `split-candidates.md` / cluster 12 note before relying on this collection for planning).

## `_allowModDetailsRefresh` — `bool`

The master "is it OK to hit the mod database right now" flag.

- **Written by:** Selected Mod / Activation & Snapshots (`SetAutoRefreshDisabled`), Mod Loading & Refresh (`LoadModsAsync` temporarily flips it around a load).
- **Read by:** Mod Loading & Refresh (`CreateModViewModel`, `RefreshModsWithErrorsAsync`), Mod Database Info Enrichment (`QueueDatabaseInfoRefresh` gate), Tag Filtering (`SetTagsColumnVisibility`/`SetUserReportsColumnVisibility`), User Reports & Voting (`EnableUserReportFetching`), Internet Access State (`OnInternetAccessStateChanged`).
- 6 clusters touch it — the widest-reaching boolean in the class. Any split has to either keep this as a shared injected "refresh policy" object or accept an event/callback fan-out.

## `_isAutoRefreshDisabled` — `bool`

The user-configured auto-refresh toggle (distinct from `_allowModDetailsRefresh`, which also accounts for forced refreshes).

- **Written by:** Selected Mod / Activation & Snapshots (`SetAutoRefreshDisabled`), constructor (from `UserConfigurationService.DisableAutoRefresh`).
- **Read by:** Fast Check & Update Polling (`FastCheck`, `ResetFastCheckTimer`, `OnFastCheckTimerElapsed`), Mod Loading & Refresh (`LoadModsAsync`).
- 3 clusters.

## `_settingsStore` — `ClientSettingsStore` (injected-ish, constructed in ctor)

Not a primitive, but a shared stateful service reference touched everywhere activation/paths are involved.

- **Touched by:** Construction, Mod Loading & Refresh (`CreateModViewModel`, `ApplyClientSettingsChangesAsync`), Selected Mod / Activation & Snapshots (`ApplyActivationChangeAsync`, `PreserveActivationStateAsync`, `GetCurrentDisabledEntries`), Preset Application (`ApplyPresetAsync`), Misc Infrastructure (`GetBasePathsList`, `PlayerUid`/`PlayerName`).
- 5 clusters. Already an injected service, so this is the *least* risky kind of hot field — extraction targets should keep depending on it via constructor injection rather than treat it as VM-private state.

## `InstalledGameVersion` — `string?` (public get-only property, set once in ctor)

- **Read by:** Fast Check & Update Polling, User Reports & Voting (every vote/etag operation), Mod Database Info Enrichment (`DetermineInstalledGameCompatibility`), Mod Loading & Refresh (`CreateModViewModel`), Selected Mod / Activation & Snapshots (`GetActiveModUsageSnapshot`).
- Immutable after construction, so it's "hot" only in the read sense — safe to pass around freely, unlike the mutable flags above.

## `ModsView` (public `ICollectionView` property over `_mods`)

- **Touched by:** View Section (`CurrentModsView` switch), Search & Filtering (`FilterMod` is its `.Filter` delegate; `RefreshModsViewIfNotCancelled` calls `.Refresh()`), Sorting (`SelectedSortOption.Apply(ModsView)`), Tag Filtering (`.Refresh()` after tag-selection changes), Preset Application (`.Refresh()` after bulk activation).
- 5 clusters call `.Refresh()`/`.Filter`/`.Apply` on it — it's the shared "please re-evaluate the grid" choke point. Any of Search, Tag Filtering, or Sorting being split out still needs write access to this one shared view.

## `_viewSection` — `ViewSection` enum

- **Owner:** View Section / Tab Navigation.
- **Also touched by:** Internet Access State (`RefreshInternetAccessDependentState` force-switches away from Database/Modlist tabs), Search & Filtering indirectly (tab switch clears search text, but that's `SetViewSection`'s own body, not a separate cluster reading the field) — counted as 2 clusters, included here mainly because it's the gate for `CurrentModsView`, itself read by 3 view-facing clusters.

## `_configuration` — `UserConfigurationService` (injected)

- **Touched by:** Construction, Mod Loading & Refresh (`CreateModViewModel` — `ShouldSkipModVersion`, `RequireExactVsVersionMatch`), Mod Database Info Enrichment (`DetermineInstalledGameCompatibility`), Mod Database Search/Browse dead cluster.
- 3 (live) clusters — another already-injected service, low risk to keep shared.

---

## Summary table

| Field | Type | Clusters touching it | Mutable? |
|---|---|---|---|
| `_mods` | `BatchedObservableCollection<ModListItemViewModel>` | 6 | yes (1 writer, 5 readers) |
| `_allowModDetailsRefresh` | `bool` | 6 | yes (2 writers, 4 readers) |
| `ModsView` | `ICollectionView` | 5 | n/a (mutated via `.Refresh()`) |
| `_settingsStore` | `ClientSettingsStore` | 5 | n/a (external service) |
| `SelectedMod`/`_selectedMod` | `ModListItemViewModel?` | 4 | yes (4 writers) |
| `_modEntriesBySourcePath` | `Dictionary<string, ModEntry>` | 4 | yes (2 writers) |
| `_searchResults` | `ObservableCollection<ModListItemViewModel>` | 4 | yes (writer effectively dead) |
| `InstalledGameVersion` | `string?` | 5 | no (ctor-only) |
| `_isAutoRefreshDisabled` | `bool` | 3 | yes (1 writer, 2 readers) |
| `_configuration` | `UserConfigurationService` | 3 | n/a (external service) |
| `_modViewModelsBySourcePath` | `Dictionary<string, ModListItemViewModel>` | 2 (but tightly paired with `_modEntriesBySourcePath`) | yes |
