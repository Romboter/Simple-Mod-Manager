# Overnight Target Hunt — MainWindow Extraction Candidates

Generated overnight via 4 parallel read-only research subagents, after the
recent path/update/install helper passes. Scope: find the next best small,
safe extraction candidates. No source files were modified to produce this
report.

Repo state at time of writing: tracked tree clean relative to HEAD
(`refactor/mainwindow-service-extractions`, ahead of origin by 1 — commit
`74df528` "refactor: extract server command copy decision", done earlier
in this session). Only pre-approved untracked paths present.

---

## Ranked candidate list

Ordered by safety + simplicity + duplication payoff (low risk, small file
count first).

### 1. `EnsureSubdirectory` — collapse 4x duplicated directory-ensure shape
- **Where:** `MainWindow.FeatureDirectories.cs` — `EnsureBackupDirectory`,
  `EnsureLocalModBackupRootDirectory`, `EnsureModListDirectory`,
  `EnsureCloudModListCacheDirectory` all repeat: combine path, `Directory.CreateDirectory`, return path.
- **Destination:** one private static helper in the same file (no new file needed).
- **Files touched:** 1 (`MainWindow.FeatureDirectories.cs`).
- **Risk:** low. `Directory.CreateDirectory` is idempotent; no behavior differs between call sites.
- **Recommendation:** implement next — simplest possible win.

### 2. `HasModConfigPath` — dedupe config-path-presence check
- **Where:** `MainWindow.ModGridSelection.cs` (`UpdateSelectedModEditConfigButton`)
  and `MainWindow.ModConfigScanning.cs` (`ScanForModConfigFilesAsync`) both
  inline the same `!string.IsNullOrWhiteSpace(modId) && TryGetModConfigPath(...) && !string.IsNullOrWhiteSpace(path)` check.
- **Destination:** `UserConfigurationService.HasModConfigPath(string? modId)`, next to existing `TryGetModConfigPath`.
- **Files touched:** 3 (`UserConfigurationService.cs`, `MainWindow.ModGridSelection.cs`, `MainWindow.ModConfigScanning.cs`).
- **Risk:** low. Pure read, same class as the method it wraps.
- **Recommendation:** implement next.

### 3. `TryGetManagedModPath` → `ManagedModPathHelper`
- **Where:** `MainWindow.ManagedModPaths.cs` — the whole method only reads
  `_dataDirectory` + `mod.SourcePath`, already delegates its hard checks
  (`IsPathWithinManagedMods`, `TryEnsureManagedModTargetIsSafe`) to `ManagedModPathHelper`.
- **Destination:** move remaining orchestration into `ManagedModPathHelper` as
  a new overload taking `(string? dataDirectory, string? sourcePath, out ..., out ...)`;
  keep a thin instance wrapper so all 5 call sites (across 4 other partials) don't change.
- **Files touched:** 2 (`MainWindow.ManagedModPaths.cs`, `Services/ManagedModPathHelper.cs`).
- **Risk:** low. Finishes a pattern already ~90% applied to sibling helpers
  (`ModInstallTargetPathHelper`, `ModUpdateTargetPathHelper` already take explicit params).
- **Recommendation:** implement next.

---

### Next up after the top 3 (still strong, slightly larger)

**4. `SingleSelectedMod` property** — dedupe `selectionCount == 1 ? _selectedMods[0] : null`
ternary between `MainWindow.ModGridSelection.cs` (`UpdateSelectedModButtons`) and
`MainWindow.ServerSync.cs` (`UpdateServerOptionsState`). Destination: new property
on the existing `ModGridSelectionService`. 3 files, low risk.

**5. `ResolveInitialConfigDirectory`** — move `GetInitialConfigDirectory` out of
`MainWindow.PathSelection.cs` into `Helpers/ModConfigPathHelper.cs` (its sibling
pure config-path helpers already live there). Converts a `_dataDirectory` field
read to an explicit parameter. 2 files, low risk. Payoff: leaves `PathSelection.cs`
100% dialog code (its stated responsibility), no stray pure logic left behind.

**6/7. Version-match pair (implement together):**
- `VersionStringUtility.FindBestVersionMatch<T>(...)` — collapses an identical
  exact-or-normalized version lookup duplicated in `MainWindow.PresetVersionApplication.cs`
  (`ApplyPresetModVersionsAsync` vs `TryInstallPresetModAsync`). 2 files, low risk.
- `PresetModVersionPlanner.BuildPlan(...)` — pure classification of preset
  entries into overrides/missing/install-candidates, currently inline in
  `ApplyPresetModVersionsAsync`. 3 files (1 new service + 1 new small model
  record). Low-medium risk only because it's a bigger lift than a one-liner —
  double-check `ModListItemViewModel` dictionary-key usage is reference
  equality (confirmed no custom `Equals`/`GetHashCode`) and that iteration
  order is preserved (plain `foreach`, so yes by construction).
  Do this right after #6 since the planner calls the new lookup directly.

### Later (real but lower priority)

- **`PresetApplyFailureSummaryBuilder`** — pure string-formatting extraction
  from `ApplyPresetModVersionsAsync`'s failure-summary block. 2 files, low
  risk, but do it after #6/#7 land to avoid re-touching the same method twice
  in flight.
- **`SelectedModButtonRules` (`CanShowSelectedModButton`/`CanShowFixButton`)**
  — pure-izes the Fix/Database/Update button decisions to match the
  already-committed `CanCopyInstallCommand` pattern. No duplication payoff
  (nothing else shares these exact conditions) — purely a consistency/testability
  win, so lower priority than anything above. Confirm a service name with the
  operator before adding a new class for this.
- **Shared `ModConfigurationCaptureRequest` builder** (`MainWindow.ModConfigCapture.cs`
  vs `MainWindow.ModlistBackups.cs`, feeding `ModConfigurationCaptureService`) —
  real duplication, but reopens the already-closed "Backup workflows" /
  "ModConfiguration workflow" milestone areas. Flag for whoever owns that
  area rather than picking up standalone.

---

## Do-not-touch-yet list

Tempting but explicitly unsafe or out of scope right now:

- **`ModUpdateDescriptor` construction duplication** — structurally identical
  across `MainWindow.ModBrowserInstallation.cs`, `MainWindow.ModDependencyRepair.cs`,
  `MainWindow.ModInstallation.cs`, `MainWindow.ModUpdateCommands.cs`, and
  `MainWindow.PresetVersionApplication.cs` (5 files). Exceeds the 4-file cap
  and spans multiple other refactor clusters — needs a cross-cutting service
  consolidation plan, not a solo extraction.
- **`TryDeleteModAtPath`** (`MainWindow.ModDeletion.cs`) — core purpose is the
  actual `Directory.Delete`/`File.Delete` plus a `WpfMessageBox.Show` on every
  branch. Destructive + dialog — leave alone.
- **Delete-confirmation message building** (`MainWindow.ModDeletion.cs`,
  `DeleteSingleModAsync`/`DeleteMultipleModsAsync`) — looks like duplication
  but unifying would touch user-facing copy/pluralization wording. Skip.
- **Load-mode status message builder** (`MainWindow.CloudLoad.cs` x2,
  `MainWindow.ModlistLoading.cs`) — three call sites use different verbs
  ("Installed"/"Loaded"/"Added") and scoping; a shared helper would require
  picking a wording template. Skip — wording-change risk.
- **Uploader-name resolution** (`MainWindow.Identity.cs` vs `MainWindow.Pdf.cs`)
  — superficially duplicated but different fallback chains and defaults
  ("Anonymous0000" vs "Anonymous"). Unifying risks a real behavior change. Skip.
- **`UpdateSyncToServerMenuState`** (`MainWindow.ServerSync.cs`) — single-use
  one-line `&&` of two already-named locals, no duplication elsewhere. Not
  worth a service method. Skip.
- **`MainWindow.ExclusivePresetApplication.cs`, `MainWindow.PresetFiles.cs`,
  `MainWindow.ModlistLoading.cs` (drag/drop wiring), `MainWindow.PathSelection.cs`
  dialogs, `MainWindow.EditConfigButton_OnClick`** — genuinely workflow/dialog-bound
  right now; nothing pure/duplicated large enough to extract without crossing
  into orchestration, I/O, or dialogs.
- **`MainWindow.ManagerDataDeletion.cs` / `ManagerDataDeletionService.cs`,
  Cloud modlist services (`CloudModlistHelper`, `CloudModlistCacheService`,
  `CloudModlistContentService`, `CloudModlistSaveService`, `CloudModlistSlotService`,
  `CloudModlistManagementService`)** — already correctly factored (thin UI
  handler over a dedicated service, or already-pure services with no
  remaining duplication found). No further extraction needed.

---

## Notes for the operator

- Candidates 1–3 above are the recommended "implement next" batch for
  tomorrow: `EnsureSubdirectory` (1 file), `HasModConfigPath` (3 files),
  `TryGetManagedModPath` → `ManagedModPathHelper` (2 files). All low risk,
  all real duplication removal, none require UI wording changes.
- No area scouted was found to be completely drained of candidates except
  where explicitly noted above (`ManagerDataDeletion.cs`, the cloud modlist
  Services classes) — those should stop getting micro-extraction attention.
