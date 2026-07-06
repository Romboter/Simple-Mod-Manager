# MVVM Roadmap — Living Document

**Created:** 2026-07-06 (Phase 2 of `2026-07-06-mvvm-area-slice-pipeline-design.md`)
**Status:** Draft — awaiting user approval of areas and slice order
**Source data:** `reports/mainwindow-responsibility/` regenerated 2026-07-06 (74 partials, 363 methods, 91 fields, 126 XAML handlers)

Every `MainWindow.*.cs` partial is assigned to exactly one area (verified: counts per area sum to 74). Line counts are current as of the analyzer re-run.

**Definition of done per area (from the design doc):** handlers thin (wire-up only), logic in services/ViewModels, moved logic covered by characterization tests, Release build zero-warnings, one user smoke test passed.

**Standing hazards for every slice:** `_isBusy`/progress-overlay/`RefreshModsAsync` coupling; dialogs (`ModManagerMessageBox`) — route through the existing `IConfirmationService`/`ConfirmationService` where a slice must decouple one, and scope dialog-flow moves explicitly per [dialog-extractions-separate-pass]; the analyzer's field-read blind spot (grep field names repo-wide, don't trust the focus report's dependency table alone).

---

## Areas

### A. Mod grid interaction & visuals — 9 partials, ~1,290 lines
`ModGridSelection` (191), `ModGridRowVisuals` (232), `ModInfoPanel` (183), `ModGridScrolling` (157), `ModGridInput` (152), `Sorting` (155), `ColumnVisibility` (82), `ModGridViewState` (67), `ModActivationInput` (67)
- **Destination:** extend `Services/ModGridSelectionService` (already owns selection state); new `ModGridSortController` for sort-state; column-visibility + view-state logic into `MainViewModel` bindings where possible. Much of RowVisuals/Scrolling/InfoPanel is genuine visual-tree code-behind (attached properties, ScrollViewer walking, drag positioning) — endpoint there is *thin and coherent*, not *empty*.
- **Hazards:** analyzer candidate #1 lives here — `UpdateSelectedMod*Button` methods are called from ModConfigScanning/ModConfiguration/ServerSync (areas F, H); moving them changes those areas' call sites. `Keyboard.Modifiers` reads. `_isModUpdateInProgress` shared with area B.
- **Manifest estimate:** the 9 partials + `Services/ModGridSelectionService.cs` + new service files + tests. Touches `MainViewModel.cs` (sort/column state).

### B. Mod operations pipeline (install / update / delete / dependency repair) — 7 partials, ~1,060 lines
`ModUpdateCommands` (326), `ModBrowserInstallation` (242), `ModDependencyRepair` (223), `ModDeletion` (203), `ModInstallation` (118), `Progress` (93), `ManagedModPaths` (52)
- **Destination:** the parked **update/install coordinator** service, built on the Phase-1-tested helpers (`ModUpdateOperationHelper`, `ModUpdateTargetPathHelper`, `ModInstallTargetPathHelper`, `ManagedModPathHelper`); finish parameterizing the `TryGetManagedModPath` remainder; progress reporting behind an injected `IProgress<>` factory instead of direct overlay manipulation.
- **Hazards:** this is the previously-rejected "too UI-bound" cluster — busy flag, progress overlay, result dialogs, `RefreshModsAsync` are all interwoven. It needs the slice to first define a progress/confirmation seam (use `IConfirmationService`), then move logic behind it. Highest-risk area; do not schedule first.
- **Manifest estimate:** the 7 partials + several `Services/` files + `MainViewModel.cs` + tests. Call sites also in areas E (preset version apply) and A (fix button).

### C. Modlists (local save / load / export) — 5 partials, ~1,010 lines
`LocalModlists` (324), `ModlistSave` (200), `ModlistLoading` (153), `ModlistLoad` (107), `Pdf` (223)
- **Destination:** `LocalModlistCatalogService`/`LocalModlistFileService`/`PdfModlistSerializer`/`InstalledModsPdfGenerator` already exist — move orchestration into a `ModlistWorkflowService` or MainViewModel commands; drag-drop stays code-behind (`ModlistDropHelper` exists).
- **Hazards:** load pipeline triggers `RefreshModsAsync` + progress UI (area B seam); save prompts dialogs.
- **Manifest estimate:** 5 partials + 1–2 `Services/` files + tests.

### D. Backups — 2 partials, ~790 lines
`DataFolderBackups` (451), `ModlistBackups` (341)
- **Destination:** extend the existing `DataFolderBackupCoordinator` (currently thin passthrough) to own restore/delete/location-change orchestration; new `ModlistBackupCoordinator` over `DataBackupService`/`BackupRetentionService`/`LocalModBackupService`. Overlay/dialog flow stays behind thin handlers.
- **Hazards:** restore flows restart-adjacent (app relaunch); backup-before-launch is called from `GameLaunch` (area I) and modlist-load (area C) — cross-area call sites.
- **Manifest estimate:** 2 partials + 2 `Services/` files + tests.

### E. Presets — 5 partials, ~930 lines
`PresetVersionApplication` (318), `PresetFiles` (274), `ExclusivePresetApplication` (148), `PresetConfigurationImport` (135), `PresetApplication` (51)
- **Destination:** `PresetFileLoader`/`PresetSnapshotBuilder`/`PresetConfigurationImportService`/`PresetConfigurationSerializer` already exist — move apply/version-apply orchestration into a `PresetApplicationService`; `TryInstallPresetModAsync` shares the area-B install seam.
- **Hazards:** `_isApplyingPreset` flag read by grid selection (area A); version application calls the update pipeline (area B).
- **Manifest estimate:** 5 partials + 1 `Services/` file + tests.

### F. Mod configuration editing — 3 partials, ~425 lines
`ModConfigScanning` (235), `ModConfiguration` (103), `ModConfigCapture` (87)
- **Destination:** `ModConfigurationMatcher`, `ModConfigPathHelper`, `ModConfigEditorViewModel` already exist — move scan/capture logic into a `ModConfigDiscoveryService`; editor-window launch stays a thin handler.
- **Hazards:** part of analyzer candidate #1 (calls `UpdateSelectedModEditConfigButton`, prompts via `PathSelection` — areas A, I). YAML parse errors surface as dialogs.
- **Manifest estimate:** 3 partials + 1 `Services/` file + tests.

### G. Cloud modlists & identity — 8 partials, ~1,340 lines
`CloudLoad` (331), `CloudManagement` (253), `CloudAuth` (193), `CloudInfrastructure` (148), `CloudSave` (146), `Identity` (71), `UserReports` (82), `InternetAccess` (21)
- **Destination:** heavy service layer already exists (`FirebaseModlistStore`, `CloudModlist*Service` family, `FirebaseAuthFileService`, `InternetAccessManager`) — move consent/orchestration flows into a `CloudWorkflowCoordinator`; identity resolution into an `IdentityService`.
- **Hazards:** consent dialogs everywhere; `DeleteCloudAuthMenuItem_OnClick` is known dead code (unwired) — leave it; auth-file lifecycle is delicate (backup/restore of `firebase-auth.json`).
- **Manifest estimate:** 8 partials + 1–2 `Services/` files + tests.

### H. Server sync — 4 partials, ~385 lines
`ServerMacro` (171), `ServerSync` (109), `ServerConnection` (62), `ServerModCopy` (42)
- **Destination:** `SyncEngine`, `ServerTargetService`, `ISftpClientWrapper`, `ServerCommandBuilder`, `ServerMacroGenerator` already exist and `ISftpClientWrapper` is already an interface — smallest gap between current state and done. Host-key verification dialog behind `IConfirmationService`.
- **Hazards:** `GenerateServerInstallMacroMenuItem_OnClick` is known dead code (unwired) — leave it; `UpdateServerOptionsState` is part of analyzer candidate #1 (calls area-A button refresh).
- **Manifest estimate:** 4 partials + tests. Few or no new services needed.

### I. Paths, profiles & game launch — 7 partials, ~950 lines
`Profiles` (237), `GameLaunch` (212), `PathSelection` (149), `PathInitialization` (136), `DeveloperProfiles` (119), `FeatureDirectories` (73), `FolderOpening` (25)
- **Destination:** `DataDirectoryLocator`/`GameDirectoryLocator`/`DeveloperProfileManager`/`InstallationPathValidator`/`FolderOpeningHelper` already exist — path resolution orchestration into a `PathBootstrapService`; profile menu logic toward MainViewModel commands.
- **Hazards:** `_dataDirectory`/`_gameDirectory` are the most-shared fields in `Core.cs` — changing how they're set ripples everywhere; launch flow calls backup gate (area D).
- **Manifest estimate:** 7 partials + 1 `Services/` file + tests. Touches `Core.cs` field usage (read-only analysis first).

### J. Manager data & self-update — 6 partials, ~825 lines
`ManagerFolder` (300), `ManagerCache` (176), `ManagerUpdateLink` (122), `ConfigurationMigration` (115), `ManagerDataDeletion` (86), `ModVersionSettings` (25)
- **Destination:** `ManagerDataFolderRelocationService`/`ManagerCacheCleanupService`/`ManagerDataDeletionService`/`ConfigurationMigrationService` already exist — mostly wiring orchestration + confirmation flows into those services' seams.
- **Hazards:** relocation/deletion flows end in app restart (`RestartApplication`) — destructive, smoke-test carefully; migration prompt runs during startup (area K ordering).
- **Manifest estimate:** 6 partials + tests.

### K. Window shell, settings & app lifecycle — 11 partials, ~1,640 lines
`Core` (457 — the shared field declarations + constructor), `Startup` (211), `Compatibility` (185), `Debugging` (178), `Navigation` (174), `Settings` (152), `Appearance` (143), `WindowState` (85), `Logging` (84), `Help` (84), `StatusReporting` (11)
- **Destination:** settings toggles become MainViewModel-bound properties over `UserConfigurationService`; window placement/theme/navigation largely stay as thin code-behind (legitimate view concerns). `Core.cs` shrinks organically as other areas absorb its fields into services — never a standalone slice.
- **Hazards:** `Startup`'s `MainWindow_Loaded`/`OnClosing` sequence orders every other area's initialization — touch last; `Debugging`/`Compatibility` open dialogs.
- **Manifest estimate:** varies; mostly small per-slice bites bundled with related areas.

### L. ViewModel lifecycle & background monitoring — 7 partials, ~1,050 lines
`ModUsage` (332), `ViewModel` (173), `ModRefresh` (147), `ModBrowserSynchronization` (105), `ViewModelPropertyChanges` (93), `ModBrowser` (71), `ModsWatcherTimer` (63)
- **Destination:** watcher/timer/session-monitor lifecycle into a `ModWatchCoordinator` service (owns `ModDirectoryWatcher`, `GameSessionMonitor`, `VotesCacheWatcher`, timer); `RefreshModsAsync` orchestration is the seam every other area depends on — defining its service boundary unblocks area B.
- **Hazards:** init/dispose ordering with `MainViewModel` lifetime; `RefreshModsAsync` is on the historical do-not-touch list — this area is where that finally gets addressed, deliberately.
- **Manifest estimate:** 7 partials + 1–2 `Services/` files + `MainViewModel.cs` + tests.

### M. MainViewModel decomposition — 1 file, 4,749 lines (continuous + final audit)
- Not a partial-relocation area: `MainViewModel.cs` is the second monolith. **Continuous rule:** no area slice may grow it — new logic goes to focused services or new small ViewModels. **Final audit slice:** once areas A–L are done, run the responsibility analyzer (or its pattern) against MainViewModel and split what remains (filtering, search, status, mod-list state are the visible clusters).
- **Hazards:** unknown internal coupling — needs its own analyzer pass before slicing; do not pre-plan the split now.

---

## Recommended slice order

Rationale: start where existing services already cover the logic (small gap, low risk, fast wins to prove the pipeline), build the progress/dialog seams mid-sequence, and only then take the hard, interwoven areas.

| # | Area | Why this position |
|---|------|-------------------|
| 1 | **H. Server sync** | Smallest area; services + interface seams already exist; Phase 1 already tests `ServerCommandBuilder` |
| 2 | **F. Mod configuration** | Small; matcher/path helpers exist; clears its share of analyzer candidate #1 |
| 3 | **D. Backups** | Coordinator pattern already proven here; extends it |
| 4 | **J. Manager data** | Services all exist; mostly orchestration wiring |
| 5 | **C. Modlists** | Medium; serializers exist; establishes the dialog-seam pattern (`IConfirmationService`) at scale |
| 6 | **E. Presets** | Depends on dialog seam; touches the install seam lightly, previewing area B |
| 7 | **G. Cloud & identity** | Large but service-backed; consent-dialog seam reuses pattern from 5–6 |
| 8 | **L. ViewModel lifecycle** | Defines the `RefreshModsAsync` + watcher seams that area B needs |
| 9 | **B. Mod operations pipeline** | The hard one — attempted only once progress/dialog/refresh seams exist (5, 8) |
| 10 | **A. Mod grid** | Interleaved with B via selection/update state; benefits from B being done |
| 11 | **I. Paths & profiles** | `_dataDirectory` ripples everywhere — safest when most consumers are already service-backed |
| 12 | **K. Window shell** + **M. MainViewModel audit** | Startup ordering last; M's final audit closes the phase |

Parallel-safety notes (design-doc rule: disjoint manifests only): 1+2 could run in parallel (H and F share only the area-A button-refresh call sites — verify before dispatch); 3+4 likewise (D and J are disjoint). Areas 5–12 are sequential by dependency.

## Maintenance log

- 2026-07-06 — Document created; awaiting approval.
