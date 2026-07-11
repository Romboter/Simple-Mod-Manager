# MainWindow Refactor Risk Map & Smoke-Test Matrix

Generated overnight via 4 parallel read-only research subagents (one completed
quickly on the first pass; the other three each ran ~6+ hours of genuine,
thorough exploration — confirmed not stuck via repeated non-blocking status
polls, see session history). No source files were modified to produce this
report.

Repo state at time of writing: tracked tree clean relative to HEAD
(`refactor/mainwindow-service-extractions`, ahead of origin by 1). Only
pre-approved untracked paths present. This report and `overnight-target-hunt.md`
(the companion extraction-candidate list from the same night) are the only
new files.

Whole-repo fact, relevant to every area below: **there is no test project
anywhere in this solution.** Every "needs tests first" and "missing test
opportunity" note below assumes bootstrapping a test project from scratch,
not adding to an existing one.

---

## Area: Mod update / install / dependency repair

- **Main partial files:** `MainWindow.ModUpdateCommands.cs`, `MainWindow.ModInstallation.cs`, `MainWindow.ModBrowserInstallation.cs`, `MainWindow.ModDependencyRepair.cs`
- **Existing extracted services/helpers:** `ModInstallTargetPathHelper`, `ModUpdateTargetPathHelper`, `ModReleaseSelectionHelper` (note: `SelectReleaseForDependency` pops a `MessageBoxButton.YesNo` directly — a UI side effect hiding inside a "service", worth flagging on its own), `ModUpdateOperationHelper`, `ModUpdateDialogHelper`, `ModUpdateService` (the real workhorse — download, zip validation, `.immbackup` rename/restore, mod-cache read/write via `ModCacheLocator`)
- **MainWindow fields used heavily:** `_isModUpdateInProgress` (global re-entrancy gate shared across install/update/dependency-repair/delete), `_viewModel`, `_dataDirectory`, `_userConfiguration` (`CacheAllVersionsLocally`, `RequireExactVsVersionMatch`), `_modUpdateService`, `_modDatabaseService`, `_modActivityLoggingService`, `_modBrowserViewModel`, `_isApplyingPreset`, modlist-install-overlay backing fields (`MainWindow.Progress.cs`)
- **UI controls/dialogs:** `UpdateModsDialog`, `BulkUpdateChangelogWindow`, `ModManagerMessageBox` (including one buried inside `ModReleaseSelectionHelper.SelectReleaseForDependency`), modlist-install progress overlay, version-selector `ComboBox`/`Popup`
- **Async workflows:** `UpdateModsAsync` (the central one — 150+ lines, per-mod loop with partial-failure bookkeeping); `InstallModButton_OnClick`/`InstallModFromBrowserAsync`; `FixModButton_OnClick` → `InstallOrUpdateDependencyAsync`; `ModUpdateService.UpdateAsync` itself
- **External side effects:** file rename/copy/extract with backup-restore-on-failure (`ModUpdateService.InstallToFile`/`InstallToDirectory`); real HTTP downloads gated by `InternetAccessManager`; mod-cache writes (`CacheAllVersionsLocally`); activity-log writes; automatic pre-update data-folder backup (zip) via `CreateAutomaticBackupAsync("ModsUpdated")`
- **Risk level: HIGH**
- **Why:** Single highest-traffic mutation path in the app — shared by single update, bulk update, version-combobox override, and dependency repair. Touches disk (rename/backup/restore), network, a global gate that fences three other features, and a large branchy method where an early `continue` skipping `requiresRefresh = true` would silently leave stale UI. `InstallToFile`/`InstallToDirectory` already implement careful try/catch/finally backup-restore logic that a careless move could break, orphaning `.immbackup` files or double-deleting mod folders.
- **Best next extraction shape:** Service coordinator, not another pure-helper pass. The target-path/release-selection pieces are already pulled into pure helpers; what's left (`UpdateModsAsync`'s loop body) is inherently stateful/UI-adjacent (progress reporting, `_isModUpdateInProgress`, dialogs) and needs a design pass — does the coordinator own `_isModUpdateInProgress`, or does MainWindow keep it and pass a callback? Does it own progress-reporter construction or take an `IProgress<T>` factory? Answer that before moving code, since 4 call sites must not diverge in behavior.
- **Smoke tests:**
  1. Update a single mod via the grid button; verify old archive replaced, activity log entry (if enabled), status bar message.
  2. Bulk "Update All Mods," cancel the dialog — verify zero mods touched, no backup created.
  3. Bulk-update 3+ mods, force one failure — verify summary dialog shows correct success/fail/skip counts, successful mods still installed.
  4. Select an older version from the per-mod version ComboBox — verify override path (no dialog) downgrades correctly.
  5. Install a mod from Mod Browser search — verify it moves to "installed" and clears from search results/selection correctly.
  6. `FixModButton_OnClick` on a mod with a missing dependency — verify install, activation, and `RefreshModsWithErrorsAsync`.
  7. Dependency repair where no DB version satisfies the minimum — verify the Yes/No "install older release anyway" prompt and both paths.
  8. Disable Internet Access, attempt an update needing a remote release — verify the friendly disabled-internet message, not a raw exception.
  9. Interrupt an update mid-download/install (induce an IOException) — verify `.immbackup` restore puts the original back with no orphaned backup file.
  10. With `CacheAllVersionsLocally` on, update a mod twice — confirm both versions land in cache and are reused on reinstall.

---

## Area: Mod deletion / managed paths

- **Main partial files:** `MainWindow.ModDeletion.cs`, `MainWindow.ManagedModPaths.cs`
- **Existing extracted services/helpers:** `ManagedModPathHelper` (pure static — `IsPathWithinManagedMods`, `TryEnsureManagedModTargetIsSafe` with symlink/reparse-point resolution), `PathRelationshipHelper`
- **MainWindow fields used heavily:** `_dataDirectory`, `_selectedMods`, `_viewModel`, `_userConfiguration` (`RemoveModConfigPath`), `_modActivityLoggingService`
- **UI controls/dialogs:** `ModManagerMessageBox` only (Yes/No delete confirmation, various error/info popups) — no custom dialogs, no progress overlay
- **Async workflows:** `DeleteSingleModAsync`/`DeleteMultipleModsAsync` await `CreateAutomaticBackupAsync("ModsDeleted")` then call the **synchronous** `TryDeleteModAtPath` (blocking `Directory.Delete`/`File.Delete` on the UI thread), then `await RefreshModsAsync()`
- **External side effects:** recursive directory/file delete; `_userConfiguration.RemoveModConfigPath` on success; activity-log write; automatic pre-delete data-folder backup (the safety net)
- **Risk level: MEDIUM**
- **Why:** Deletion is irreversible except via the automatic backup, and `ManagedModPathHelper` is the only thing standing between a mistake and `Directory.Delete(path, true)` — reordering its containment check relative to the symlink-resolution check in a future refactor could reopen a delete-outside-Mods-folder hole. Not HIGH because there's no network/async-race surface and the code paths are short and already well-isolated.
- **Best next extraction shape:** Leave alone / at most a thin coordinator. The pure safety logic is already extracted; `TryDeleteModAtPath` is small enough that further splitting would just relocate ~15 lines without reducing risk. **Mostly drained** — good candidate to mark "reviewed, no further micro-extraction" like `GameLaunch.cs`/`ModUsage.cs` were.
- **Smoke tests:**
  1. Delete a single file-based mod (`.zip`) — confirm removal, prior backup created, config association cleared.
  2. Delete a single folder-based (unpacked) mod — confirm recursive removal.
  3. Multi-select 3+ and bulk-delete — verify confirmation dialog lists them (with truncation past 10) and only valid-path mods are removed.
  4. Attempt to delete a mod whose source path resolves outside `Mods`/`ModsByServer` — verify the block message and no deletion.
  5. Attempt to delete a symlink pointing outside the managed directory — verify the symlink-specific block and the link left untouched.
  6. Delete a mod already externally removed from disk — verify graceful "already removed" message, no crash.
  7. Answer "No" on the delete confirmation — verify zero side effects (no backup, no delete, no log entry).
  8. Force a delete failure (lock the file in another process) — verify the IOException path reports failure honestly, doesn't falsely log success.
  9. Confirm `RefreshModsAsync` removes the mod from the grid without an app restart.
  10. Restore from the automatic "ModsDeleted" backup taken just before a deletion — confirm the deleted mod reappears correctly.

---

## Area: Manager data / cache / data-folder operations

- **Main partial files:** `MainWindow.DataFolderBackups.cs`, `MainWindow.ManagerCache.cs`, `MainWindow.ManagerDataDeletion.cs`, and **`MainWindow.ManagerFolder.cs`** (not in the original file list but squarely in this area — folder relocation/reset/restart)
- **Existing extracted services/helpers:** `DataFolderBackupCoordinator` (thin wrapper around the larger `DataBackupService`), `ManagerCacheCleanupService`, `ManagerDataDeletionService`, `ManagerDataFolderRelocationService`
- **MainWindow fields used heavily:** `_dataDirectory`, `_gameDirectory`, `_dataFolderBackupCoordinator`, `_userConfiguration`, `_viewModel`, `MaxDataBackupsMenuItems`
- **UI controls/dialogs:** dynamically-built menu items with live headers (cache size, backup count), Windows Recycle Bin dialogs (via `Microsoft.VisualBasic.FileIO`), `FolderBrowserDialog`, custom `ChangeManagerFolderDialog`, `ModManagerMessageBox`, a bound progress overlay (`IsDataBackupInProgress`/`DataBackupProgress`/`DataBackupStatusMessage`)
- **Async workflows:** `RestoreDataBackupAsync` (awaits `_dataFolderBackupCoordinator.RestoreBackupAsync` then `RefreshModsAsync(true)`); `RefreshDeleteCachedModsMenuHeaderAsync` (fires on every `_Mods` menu open — hot path); `DeleteAllManagerFilesMenuItem_OnClick` (awaits a Firebase cloud-data delete, then `Task.Run(DeleteAllManagerFiles)`)
- **External side effects:** permanent (non-recycle-bin) `Directory.Delete` for cache folders; whole-VintagestoryData-folder replacement on restore; Firebase account/cloud-data deletion before local file deletion; **process restart** (`Process.Start` + `Application.Current.Shutdown()`) tied to a folder move/reset; settings writes (`SetCustomDataBackupLocation`, `CustomConfigFolderManager`)
- **Risk level: HIGH**
- **Why:** Irreversible whole-tree destructive operations (permanent cache delete, VintagestoryData replacement) plus a Firebase account-data delete plus a process restart tied to a directory move. `ManagerFolder.cs`'s move/reset workflow has no rollback if `RestartApplication` fails mid-flight after a partially-successful `MoveDirectoryContents`.
- **Best next extraction shape:** Mixed. `DataFolderBackups.cs`/`ManagerCache.cs`/`ManagerDataDeletion.cs` are already thin glue over real services — leave alone. `ManagerFolder.cs` **needs a service coordinator**: the confirm → merge-check → move → restart sequence is duplicated almost verbatim between "change folder" and "reset to default" (~120 duplicated lines) — a `ManagerFolderRelocationCoordinator` (paralleling `DataFolderBackupCoordinator`'s naming) would collapse this. `ManagerDataDeletion.cs` **needs tests first**, not more extraction — it's the single scariest button in the app (nukes cloud auth + every local file) with zero coverage of its path-candidate logic.
- **Smoke tests:**
  1. Open `_Mods` menu repeatedly — confirm the "Delete Cached Mods (NNMb)" header updates without hitching the UI thread.
  2. With 2+ backups present, open "Restore Data Folder" — confirm only backups matching current data dir + installed VS version show.
  3. Restore a backup — confirm progress overlay shows/clears, mods refresh, and the folder actually matches the restored contents.
  4. "Delete all data folder backups" with a version mismatch — confirm it refuses.
  5. "Delete Cached Mods" — confirm the cache directory is gone but installed mods/modlists untouched.
  6. "Clear All Caches" — confirm Temp Cache + legacy cache folders gone, other settings/presets survive.
  7. "Change manager folder location" to a new empty path — confirm restart, and on relaunch the manager reads from the new location.
  8. "Reset manager folder to default" — confirm merge-prompt if default pre-exists, confirm restart, confirm old location correctly emptied/merged.
  9. "Delete All Manager Files" in a throwaway test profile — confirm Firebase data cleared *before* local files, confirm items land in Recycle Bin (not permanently deleted), confirm both `DeletedPaths`/`FailedPaths` reported correctly when one path is locked.

---

## Area: Presets / version application / exclusive preset apply

- **Main partial files:** `MainWindow.PresetApplication.cs`, `MainWindow.PresetVersionApplication.cs`, `MainWindow.ExclusivePresetApplication.cs`, `MainWindow.PresetFiles.cs`, `MainWindow.PresetConfigurationImport.cs`
- **Existing extracted services/helpers:** `VersionStringUtility` (pure, already has internal caching), `PresetFileLoader`, `PresetSnapshotBuilder`, `PresetConfigurationSerializer`, `PresetConfigurationImportService`
- **MainWindow fields used heavily:** `_viewModel`, `_isApplyingPreset` (**cross-cutting** — also read by `GameLaunch.cs`, `ModGridInput.cs`, `ModGridSelection.cs`, `ModlistLoading.cs`, `ModUpdateCommands.cs`, `Startup.cs`), `_recentLocalModBackupDirectory`/`_recentLocalModBackupModNames`, `_dataDirectory`, `_userConfiguration`, `_modDatabaseService`, `_modUpdateService`, `_modActivityLoggingService`
- **UI controls/dialogs:** `SaveFileDialog`/`OpenFileDialog`, dynamic preset-list `MenuItem`, `ModManagerMessageBox` confirmations, modlist-install progress overlay
- **Async workflows:** `ApplyPresetAsync` (top orchestrator) → `ApplyPresetModVersionsAsync` → `TryInstallPresetModAsync` (per missing mod) → `_modDatabaseService.TryLoadDatabaseInfoAsync` + `ModUpdateOperationHelper.ExecuteAsync`; `ApplyExclusivePresetAsync` → `RefreshModsAsync(true)`; `ImportPresetConfigsAsync` → `PresetConfigurationImportService.ImportAsync`
- **External side effects:** **`ApplyExclusivePresetAsync` deletes real installed mod files/directories** driven by a loaded JSON preset's keep-set, with a *conditional* backup-before-delete (`LocalModBackupService.BackupLocalModAtPath`, only when `sourceExists && !mod.HasModDatabasePageLink && !localBackupInitializationFailed` — worth verifying this can't silently skip a backup it should take); network calls for mod-database lookup + download; preset JSON writes; per-mod config file writes; settings writes (`SetLastSelectedPresetName`, config path writes)
- **Risk level: HIGH** (specifically `ExclusivePresetApplication.cs`; rest of the cluster is medium)
- **Why:** A malformed or wrong-context preset applied in exclusive mode deletes real user mod files based entirely on file contents, with a conditional (and therefore breakable) backup safety net, combined with live network installs — all under one `_isApplyingPreset` flag read by six unrelated files.
- **Best next extraction shape:** `ExclusivePresetApplication.cs` **needs tests first, not more movement** — the keep-set/backup/delete decision is pure enough to test if the direct `Directory.Exists`/`File.Exists`/`Directory.Delete` calls were behind a seam; the honest next step is a `PresetExclusiveApplyPlanner` (pure plan) + thin I/O executor, which is a design task, not a quick move. `PresetVersionApplication.cs` (318 lines, largest file) is a reasonable **service coordinator** candidate — the mod-lookup/desired-version/missing-version triage logic is close to pure and could become `PresetVersionResolutionService.Plan(...)`. `PresetFiles.cs`/`PresetConfigurationImport.cs`/`PresetApplication.cs` are already reasonably drained — thin glue over real services.
- **Smoke tests:**
  1. Save a non-exclusive preset from the current modlist, confirm the file appears and reopens/inspects correctly.
  2. Load it back — confirm active/inactive states match, no mods deleted.
  3. Build an exclusive preset with a subset of installed mods, load it — confirm mods not in the preset are removed, mods without a DB link get a local backup first, correct removal-count status message, refresh afterward.
  4. Repeat with a mod file locked by another process — confirm per-mod failure reporting rather than aborting the whole operation.
  5. Load a preset referencing a version not currently installed — confirm install-via-overlay, and a nonexistent version produces a clear message, not a crash.
  6. Load a preset with mod IDs absent from the mod database entirely — confirm clear "could not be installed" messaging with backup-directory info surfaced.
  7. Load a preset with an embedded mod configuration — confirm the overwrite Yes/No prompt, and that No correctly skips the import.
  8. Attempt to load a `.json` from outside the Presets folder via the file dialog — confirm the folder guard actually blocks it.
  9. If a legacy PDF-based preset sample exists, confirm the PDF-metadata fallback path still round-trips.
  10. During a long preset-apply, attempt to launch the game or interact with the mod grid — confirm `_isApplyingPreset` correctly blocks those actions until done.

---

## Area: Mod grid selection / config scanning / config editing / server sync

- **Main partial files:** `MainWindow.ModGridSelection.cs`, `MainWindow.ModConfigScanning.cs`, `MainWindow.ModConfiguration.cs`, `MainWindow.PathSelection.cs`, `MainWindow.ServerSync.cs`
- **Closely adjacent (load-bearing, worth tracking alongside this area):** `MainWindow.ModGridInput.cs` (actual call sites for selection methods — Ctrl+A/Delete/click handling), `MainWindow.ServerModCopy.cs` (clipboard copy), `MainWindow.ServerConnection.cs` (SFTP/host-key plumbing)
- **Existing extracted services/helpers:** `ModGridSelectionService` (owns selection state + per-mod `PropertyChanged` subscriptions), `ServerCommandBuilder` (`TryBuildInstallCommand`/`CanCopyInstallCommand` — the commit `74df528` extraction, the template for this cluster's low-risk-extraction pattern), `ModConfigPathHelper` (in `Helpers/`, not `Services/`), `ModConfigurationMatcher` (Levenshtein-based fuzzy matcher, pure), `UserConfigurationService` config-path + server-profile methods
- **MainWindow fields used heavily:** `_modSelection`, `_userConfiguration`, `_viewModel`, `_dataDirectory`, `_isApplyingPreset`, `_isModUpdateInProgress`, `_serverTargetService`, `_syncEngine`, plus direct references to `SelectedModDatabasePageButton`/`SelectedModUpdateButton`/`SelectedModEditConfigButton`/`SelectedModDeleteButton`/`SelectedModFixButton`/`SelectedModCopyForServerButton`/`ModsDataGrid`/`SyncToServerMenuItem`/`EnableServerOptionsMenuItem`
- **UI controls/dialogs:** selected-mod button row on the DataGrid, `ModConfigEditorWindow`/`ModConfigEditorViewModel` (modal), WinForms `OpenFileDialog`/`FolderBrowserDialog` (mixed into a WPF app), `ManageServerTargetsDialog`, `SyncToServerDialog`, `ModManagerMessageBox`
- **Async workflows:** `ScanForModConfigsMenuItem_OnClick` → `ScanForModConfigFilesAsync` → `Task.Run(ModConfigurationMatcher.FindConfigMatches)` (CPU-bound work offloaded, then resumes on UI thread with `ConfigureAwait(true)` to persist and update button state); `SelectDataFolderMenuItem_OnClick` → `ReloadViewModelAsync`; `TryHandleModListKeyDownAsync` → `DeleteSelectedModsAsync`. `EditConfigButton_OnClick` and all `ServerSync.cs` handlers are synchronous (blocking `ShowDialog()`).
- **External side effects:** config-path persistence writes (`_userConfiguration.SetModConfigPath`/`RemoveModConfigPath`) as a side effect of what looks like a read-only "scan"; config file open/edit read-writes the actual mod config `.json`/`.yaml`/`.yml`; server-target/enable-options settings writes; SFTP network I/O is triggered here but happens inside the dialog/`SyncEngine`, not in these partials directly; clipboard write (adjacent `ServerModCopy.cs`)
- **Risk level: MEDIUM** (heterogeneous, not uniform)
- **Why:** `ModGridSelection.cs` is low risk — already a thin facade over `ModGridSelectionService`, mostly delegation. `ModConfigScanning.cs` is medium-to-higher — 3 overloaded async methods with shared mutable-state traversal, a background-thread hop, and a "scan" that silently mutates persisted config-path state. `ModConfiguration.cs` is medium — one method juggling config-path mutation, dialog lifecycle, and 3 separate try/catch recovery paths that are easy to silently break. `ServerSync.cs` is low-to-medium — mostly guard-clause validation and dialog launch; the real risk lives in what it wires (SFTP), not in the code itself.
- **Best next extraction shape:** `ModGridSelection.cs` — leave alone / tiny pure-helper extractions only (see `overnight-target-hunt.md` for the 4 already-found candidates: `HasModConfigPath` dedup, `SingleSelectedMod` dedup, `ResolveInitialConfigDirectory` move, `SelectedModButtonRules`). `ModConfigScanning.cs` **needs a service coordinator** — right now scanning logic is split four ways (WPF handler → 3-overload method cluster → `ModConfigPathHelper`/`ModConfigurationMatcher` → back into `UserConfigurationService` for persistence) with no single owner; that's the kind of thing CLAUDE.md's "Up Next" flags as needing its own scoping pass. `ModConfiguration.cs` **needs tests first** — characterize the 3 recovery paths (stale-path pruning, first-time prompt, post-edit persistence, editor-exception rollback) before touching. `ServerSync.cs` — leave alone, thin wiring layer.
- **Smoke tests:**
  1. Click a mod row — verify correct per-mod button state (Edit Config/Delete/Fix/Copy-for-Server/Database-page).
  2. Ctrl+click 2-3 more rows — verify multi-select hides Edit Config/Fix/Copy-for-Server but Delete stays visible/enabled.
  3. Shift+click for range-select — verify contiguous selection and anchor update.
  4. Ctrl+A with grid focus vs. TextBox focus — verify select-all-in-view vs. normal text selection respectively.
  5. Clear selection via empty-area click / Escape — verify anchor resets.
  6. Switch tabs/filters — verify previously-selected mods not in the new view drop out of selection gracefully.
  7. Scan for Mod Configs with no data directory set, then with no `ModConfig` folder — verify the two distinct informational dialogs.
  8. Scan with a populated `ModConfig` folder and unassigned mods — verify correct fuzzy matches, dialog listing, and that config paths actually persist across an app restart.
  9. Re-run the scan immediately — verify idempotent "no missing configs found" result.
  10. "Set Config..." on a mod with no path — verify the filtered OpenFileDialog, and cancel produces no state change.
  11. Edit and save a config — verify status message and button label flip to "Edit Config".
  12. Point a stored config path at a since-deleted file, click Edit Config — verify stale-path pruning + fallback prompt instead of a crash.
  13. Force a malformed config file — verify the JSON/YAML exception path shows an error and removes the (now-broken) path rather than leaving it half-set.
  14. Non-Server profile → "Sync to Server" — verify the server-profile-only info dialog.
  15. Server profile with no target configured → verify the "no server target" warning.
  16. Configure a target via "Manage Server Targets," close the dialog — verify `SyncToServerMenuItem.IsEnabled` updates immediately.
  17. Toggle "Enable Server Options" — verify menu items/separators and the Copy-for-Server button all react immediately without reselection.
  18. Select a mod with server options enabled and a valid ModId+Version, click Copy-for-Server — verify clipboard contents and status message.

---

## Area: Modlists (local/cloud load/save)

- **Main partial files:** `MainWindow.CloudLoad.cs`, `MainWindow.CloudSave.cs`, `MainWindow.LocalModlists.cs`, `MainWindow.ModlistLoad.cs` (distinct from `ModlistLoading.cs` — file-open/drag-drop path), `MainWindow.ModlistLoading.cs` (shared load-mode prompting/backup-before-load), `MainWindow.ModlistSave.cs`, `MainWindow.ModlistBackups.cs` (actually the automatic/app-start backup pipeline, not modlist-save-specific), `MainWindow.FeatureDirectories.cs`
- **Existing extracted services/helpers:** `CloudModlistHelper`, `CloudModlistCacheService`, `CloudModlistContentService`, `CloudModlistSaveService`, `CloudModlistSlotService`, `CloudModlistManagementService`, `ModConfigurationCaptureService`, `LocalModlistCatalogService`, `LocalModlistFileService`, `PdfModlistSerializer`, `FirebaseModlistStore`, `FirebaseModlistMigrationService`, `ModlistMetadataParser`, `BackupRetentionService`, `PresetSnapshotBuilder`, `PresetFileLoader`
- **MainWindow fields used heavily:** `_viewModel`, `_selectedCloudModlist`, `_cloudModlistsLoaded`, `_isCloudModlistRefreshInProgress`, `_selectedLocalModlists`, `_localModlistsLoaded`, `_userConfiguration`, `_dataDirectory`, `_backupSemaphore`
- **UI controls/dialogs:** `CloudModlistsDataGrid`/`LocalModlistsDataGrid` and their selection-summary panels, various cloud/local action buttons, `CloudSlotSelectionDialog`, `CloudModlistDetailsDialog`, `LocalModlistEditDialog`, `SaveInstalledModsDialog`, `RestoreBackupDialog`, `OpenFileDialog`
- **Async workflows:** `RefreshCloudModlistsAsync` → `ExecuteCloudOperationAsync` → `store.GetRegistryEntriesAsync`; `InstallCloudModlistButton_OnClick` → content-fetch → cache write → automatic backup → `ApplyPresetAsync` (heavy downstream install/uninstall — out of this area's scope but the critical dependency); `SaveModlistToCloudAsync` → `CloudModlistSaveService`; `LoadModlistFromFileAsync` → `PresetFileLoader` (sync) → backup → `ApplyPresetAsync`; `CreateBackupAsync`/`RestoreBackupAsync` → semaphore-guarded file write/apply
- **External side effects:** cloud-modlist JSON cache writes; local modlist JSON writes/deletes/in-place rewrites; backup JSON writes + retention pruning; every `Cloud*` method routes through Firebase HTTP calls; a second, independent registry-cache file layer inside `FirebaseModlistStore` itself; the downstream `ApplyPresetAsync` call is the single highest-impact side effect any of these workflows can trigger (adds/removes installed mods)
- **Risk level: MEDIUM**
- **Why:** No process restarts or destructive OS-level ops live directly in these files, but every load path funnels into `ApplyPresetAsync` — a wrong load-mode or stale/partial cloud JSON can silently replace or corrupt the user's mod list. The backup-before-load gate is the main safety net and easy to accidentally bypass while reordering checks during a refactor. Network dependency adds nondeterminism to manual testing. **Two dead-code, XAML-unwired handlers exist here** (`LoadModlistFromCloudMenuItem_OnClick`, `DeleteCloudModlistMenuItem_OnClick`) — a trap for anyone assuming every method is reachable; don't "helpfully" wire them up as a refactor side effect.
- **Best next extraction shape:** Mostly leave alone / pure-helper only — remaining methods are UI-orchestration glue (dialog prompts, button-enable-state, status reporting) tightly bound to specific WPF controls, genuinely UI-bound until an MVVM pass. One clean exception: `TryBuildCurrentModlistJson` (`MainWindow.ModlistSave.cs:173`) has zero WPF/control dependency and could move to a service now. **Mostly drained** for the current relocate-methods phase — what's left needs actual MVVM boundaries, not more file-splitting.
- **Smoke tests:**
  1. Save a local modlist (with and without configs included) — confirm the file and its metadata are correct.
  2. Load a local modlist via menu and via drag-and-drop; verify both Replace/Add modes, and that Replace prompts a backup first.
  3. Save to an empty cloud slot, then again to an occupied slot — verify replace-confirmation and slot-selection dialogs.
  4. Load from the cloud dialog with both empty and populated slot states.
  5. Delete a local modlist (single and multi-select) — confirm confirmation dialog, file removal, grid refresh.
  6. Disable internet access — confirm all cloud controls grey out and show the disabled-internet message instead of throwing.
  7. Disconnect network mid-cloud-operation — confirm graceful error dialog + status log entry, no crash.
  8. Trigger an automatic backup and confirm retention pruning doesn't remove more than intended.
  9. Confirm the two dead-code cloud handlers remain unreachable from the UI if this area is ever touched.

---

## Area: Cloud auth / infrastructure / management (+ voting, cross-checked)

- **Main partial files:** `MainWindow.CloudAuth.cs`, `MainWindow.CloudInfrastructure.cs`, `MainWindow.CloudManagement.cs`, `MainWindow.InternetAccess.cs`
- **Voting confirmed present but NOT in these four files:** `ModVersionVoteService`, `VotesCacheWatcher`, `ModVoteDialog.xaml.cs`/`ModVoteReasonDialog.xaml.cs`, `MainWindow.UserReports.cs`. The only touchpoint is `EnsureUserReportVotingConsent` (`CloudAuth.cs:55`), a consent-gate helper called from `UserReports.cs`. **Real hidden coupling found:** cloud modlists and mod-compat voting share one physical `firebase-auth.json` file, even though they're served by two independently-constructed `FirebaseAnonymousAuthenticator`/store instances.
- **Existing extracted services/helpers:** `FirebaseModlistStore` (1098 lines, constructed/cached here), `FirebaseModlistMigrationService`, `FirebaseAuthFileService`, `FirebaseAnonymousAuthenticator` (in `SimpleVsManager.Cloud`, not `Services/`), `CloudModlistManagementService`, `CloudModlistHelper`, `InternetAccessManager`
- **MainWindow fields used heavily:** `_cloudModlistStore` (lazily created, cached), `_cloudStoreLock` (guards lazy-init), `_firebaseMigrationAttempted` (one-shot flag), `_dataDirectory`, `_modActivityLoggingService`, `_viewModel`
- **UI controls/dialogs:** `ModManagerMessageBox`, `CloudModlistManagementDialog`; `InternetAccess.cs` is a thin cross-cutting event relay (reaches into `UpdateCloudModlistControlsEnabledState()` and `RefreshManagerUpdateLinkAsync()` owned elsewhere), not itself UI-bound
- **Async workflows:** `ExecuteCloudOperationAsync` (central try/catch wrapper every cloud call routes through — awaits store-init, the operation, then a sync auth-file backup on success); `EnsureCloudStoreInitializedAsync` (double-checked locking over `_cloudStoreLock`); `MigrateLegacyFirebaseDataIfNeededAsync` (one-shot, gated by `_firebaseMigrationAttempted`, shows a one-time migration dialog); `CloudManagement.cs`'s rename/delete/load-entries paths
- **External side effects:** `firebase-auth.json` create/copy/delete (backup, restore, delete-both-copies); Firebase network calls including **`DeleteAllCloudModlistsAndAuthorizationAsync` → `Authenticator.DeleteAccountAsync`** (deletes the account server-side, not just local files); two independent process-lifetime static `HttpClient` instances (`FirebaseModlistStore` and `ModVersionVoteService`) that any refactor must not accidentally duplicate/leak; consent is implicit ("has this file ever been created"), not a settings flag — easy to miss; `DeleteCloudAuthMenuItem_OnClick` confirmed dead code (no XAML wiring), consistent with the prior CLAUDE.md note
- **Risk level: HIGH**
- **Why:** `DeleteAllCloudModlistsAndAuthorizationAsync` is a genuine point-of-no-return — deletes server-side Firebase data *and* the account, then local auth files. It sits behind lazy double-checked-locking and a one-shot migration flag that are easy to break subtly during extraction (e.g. moving the lock but not the field triad together, or losing the "only migrate once" guarantee). The cross-feature coupling to voting via a shared auth file isn't visible from reading `CloudAuth.cs` alone.
- **Best next extraction shape:** **Needs an explicit service-boundary plan before any more code movement.** No single owner currently exists for "the Firebase auth lifecycle" — existence-check lives in `CloudAuth.cs`, backup/restore is split across `CloudAuth.cs` and `FirebaseAuthFileService`, deletion is called from `CloudManagement.cs`, and the file is independently read by `FirebaseModlistStore`'s and `ModVersionVoteService`'s own authenticator instances, plus a *fourth*, unrelated path via `ManagerDataDeletionService`'s "wipe all manager data" feature. Define a `FirebaseAuthLifecycleService` (owning path resolution, backup, restore, delete as one unit) before moving any more of the surrounding glue. `ExecuteCloudOperationAsync`/`EnsureCloudStoreInitializedAsync` are legitimate coordinator-extraction targets *if and only if* the `_cloudStoreLock`/`_cloudModlistStore`/`_firebaseMigrationAttempted` field triad moves together, atomically, never split across two commits.
- **Smoke tests:**
  1. Fresh profile, no auth file — open the Modlists (Beta) tab, confirm consent dialog appears exactly once and creates the file on OK.
  2. Decline consent — confirm cloud features stay disabled and no file is created.
  3. Restart the app twice with an existing auth file — confirm consent does not reappear.
  4. Restore-backup menu item with backup present, then absent — confirm all 4 distinct result messages.
  5. Save a cloud modlist, then manually delete the auth file — confirm the next cloud op re-triggers consent instead of crashing.
  6. Cast a mod-compat vote, then restore/delete the auth file — confirm vote ownership is affected too (shared-file coupling confirmed).
  7. Verify `MigrateLegacyFirebaseDataIfNeededAsync` runs only once per session even with concurrent triggers at startup — watch for duplicate migration dialogs.
  8. Fire two cloud operations back-to-back at startup — confirm `_cloudStoreLock` prevents duplicate store construction/double migration/double consent dialog.
  9. Before ever re-wiring the dead `DeleteCloudAuthMenuItem_OnClick`, test the full delete-all-cloud-data-and-authorization path against a disposable test account first — this is the single most destructive action in the app.
  10. Toggle internet-access-disabled mid-session — confirm cloud controls and the manager-update link correctly re-enable/refresh without a restart.

---

## Cross-cutting summary

### Areas mostly drained (stop micro-extracting)
- **Mod deletion / managed paths** — pure safety logic already extracted; further splitting is pure code-shuffling.
- **Manager data / cache** (excluding `ManagerFolder.cs`) — already thin glue over real services.
- **Preset files / config import / preset-apply shell** (`PresetFiles.cs`, `PresetConfigurationImport.cs`, `PresetApplication.cs`) — thin dialog/glue over already-substantive services.
- **Modlists local/cloud load/save** — no more misplaced methods to relocate; what remains needs MVVM, not file-splitting.
- **`MainWindow.ModGridSelection.cs`** (selection facade specifically, not the whole cluster) — already a clean facade, only tiny helper dedup candidates left (tracked in `overnight-target-hunt.md`).

### Areas tempting but too UI-bound right now
- Most of the **Modlists local/cloud** cluster (`CloudLoad.cs`, `LocalModlists.cs`) — individually short methods that look extractable but nearly every one directly touches a named grid/button/textblock; pulling logic out without the control references just relocates the coupling.
- **`ModGridSelection.cs`'s button-visibility methods** — extracting the decision logic (the established `CanCopyInstallCommand` pattern) is safe; extracting the control-manipulation itself would just relocate UI code until an MVVM pass introduces bindings.
- The **update/install overlay wiring** inside `UpdateModsAsync` — genuinely coupled to progress/dialog callbacks; a coordinator extraction here needs those passed as parameters, not a quick win.

### Areas needing an explicit service-boundary plan before code movement
- **Cloud auth/Firebase lifecycle** — no single owner across `CloudAuth.cs`/`FirebaseAuthFileService`/`CloudManagement.cs`/`ManagerDataDeletionService`/the voting feature's independent authenticator. Highest-priority design gap found this session.
- **`UpdateModsAsync`'s coordinator extraction** — needs a decision on who owns `_isModUpdateInProgress` and progress-reporter construction before any code moves, since 4 call sites must stay in sync.
- **`ExclusivePresetApplication.cs`** — the backup-then-delete branching needs a pure-plan/execute split designed deliberately, not extracted ad hoc; a hasty move risks silently changing which mods get backed up before deletion.
- **`ModConfigScanning.cs`** — scanning logic is currently split four ways (handler → method cluster → 2 helper classes → `UserConfigurationService`) with no single coordinator owning the workflow.
- **`ManagerFolder.cs`** — lower priority than the above, but the confirm/move/restart sequence is ~120 lines duplicated between two entry points and would benefit from one coordinator rather than being fixed via a quick copy-paste-unify.

### Missing automated-test opportunities (highest value, cheapest setup)
No test project exists anywhere in the repo — everything below has zero coverage because there's no harness yet, not because of gaps in an existing suite.
- **`VersionStringUtility`** (`Normalize`/`IsCandidateVersionNewer`/etc.) — 100% pure static string logic, zero I/O, zero WPF dependency, has internal caching that could itself regress silently. Single cheapest, highest-value place to start a test project.
- **`ServerCommandBuilder`** (`TryBuildInstallCommand`/`CanCopyInstallCommand`) — pure, zero dependencies, trivial to cover (null/whitespace inputs, valid combo, server-options-disabled case).
- **`ModConfigurationMatcher.FindConfigMatches`** — pure but real algorithmic complexity (Levenshtein + camelCase tokenization + fallback), exactly the kind of logic prone to silent regression; zero coverage today.
- **`ManagedModPathHelper`** (`IsPathWithinManagedMods`/`TryEnsureManagedModTargetIsSafe`) — pure, static, and gates the actual delete-safety boundary; cheap to test with real temp directories/symlinks, high safety value.
- **`ModConfigPathHelper`** (`GetSafeConfigFileName`/`NormalizeRelativeConfigPath`/`EnsureUniqueRelativePath`) — pure string/path manipulation, cheap.
- **`FirebaseAuthFileService`** (`RestoreFirebaseAuthBackup`/`TryDeleteFirebaseAuthFile`) — pure file-path logic with a well-defined result enum, testable against a temp directory, and directly implicated in the "lost access to cloud data" recovery flow — high real-world stakes, zero coverage.
- **`CloudModlistHelper`/`CloudModlistManagementService`** — pure string/JSON transforms and result-status branching, easy to mock the one seam (`FirebaseModlistStore`).
- **`PresetConfigurationImportService.CollectConfigurations`, `PresetSnapshotBuilder`, `PresetConfigurationSerializer`** — all pure, cheap to test with synthetic data.
- Everything with real file I/O (`ManagerDataDeletionService`, `ManagerCacheCleanupService`, `PresetFileLoader`, `PresetConfigurationImportService.ImportAsync`) needs a temp-directory-based integration test rather than a pure unit test — still worthwhile, bigger lift.

---

## Terminal-summary picks

**Top 5 safest future extraction zones** (low risk, clear shape, small blast radius):
1. Mod deletion / managed paths — mostly drained, safe to leave, only `TryGetManagedModPath` → `ManagedModPathHelper` remains (see `overnight-target-hunt.md`).
2. `MainWindow.ModGridSelection.cs` selection facade — clean, thin, only small dedup helpers left.
3. `TryBuildCurrentModlistJson` (`ModlistSave.cs`) — zero WPF dependency, clean pure-helper move.
4. `DataFolderBackups.cs`/`ManagerCache.cs` glue — already thin, stable, low-risk to leave or lightly touch.
5. `PresetFiles.cs`/`PresetConfigurationImport.cs`/`PresetApplication.cs` — thin dialog glue over substantive services, low risk if touched at all.

**Top 5 highest-risk zones** (handle with a design pass, not ad hoc):
1. Cloud auth/Firebase lifecycle — no single owner across 4 independent code paths sharing one file; account-deletion is the single most destructive action in the app.
2. `MainWindow.ManagerDataDeletion.cs` / `ManagerDataDeletionService` — nukes cloud auth + all local files; zero test coverage.
3. `UpdateModsAsync` (mod update/install pipeline) — highest-traffic mutation path, shared by 4 features, backup/restore-on-failure logic that's easy to subtly break.
4. `ExclusivePresetApplication.cs` — deletes real mod files driven by preset JSON content, with a conditional (breakable) backup safety net.
5. `MainWindow.ManagerFolder.cs` — directory move/reset tied to a process restart with no rollback if the restart fails mid-flight.

**Recommended smoke-test checklist for the next checkpoint** (minimum viable pass across all 7 areas before merging any batch of extractions):
- Single + bulk mod update, one forced failure case.
- Single mod install from Mod Browser.
- Dependency repair (fix button) on a mod with a missing dependency.
- Single + multi-select mod deletion, plus one "answer No" cancellation.
- Data-folder backup restore + cache deletion.
- Exclusive preset load that removes at least one mod (verify backup-before-delete fires).
- Local modlist save/load round-trip (both Replace and Add modes).
- Cloud modlist save/load round-trip (with internet toggled off for one negative case).
- Firebase auth backup/restore/delete-all — run delete-all only against a disposable test account.
- Mod grid multi-select + config scan + config edit round-trip.
- Server sync menu state toggle (enable/disable server options) and Copy-for-Server clipboard check.
