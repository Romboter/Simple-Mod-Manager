# Focused Report: MainWindow.ModGridSelection.cs

## Header

- **Focused partial file:** MainWindow.ModGridSelection.cs
- **Line count:** 192
- **Method count:** 14
- **Event handler count:** 1

## Method Inventory

### AddToSelection

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modSelection
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### ClearModDatabaseSelections

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modSelection
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### ClearSelection

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modSelection
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### CloseModInfoButton_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** Button.Click
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ClearSelection
- **Awaited calls:** (none)
- **External types used:** (none)

### GetModsInViewOrder

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### HandleModRowSelection

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _isApplyingPreset;_modSelection
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** GetModsInViewOrder
- **Awaited calls:** (none)
- **External types used:** (none)

### RemoveFromSelection

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modSelection
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### RestoreSelectionFromSourcePaths

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modSelection;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### SelectAllModsInCurrentView

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _isApplyingPreset;_modSelection
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** GetModsInViewOrder
- **Awaited calls:** (none)
- **External types used:** (none)

### UpdateSelectedModButton

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### UpdateSelectedModButtons

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** SelectedModDatabasePageButton;SelectedModDeleteButton;SelectedModUpdateButton;_selectedMods
- **MainWindow methods called:** UpdateSelectedModButton;UpdateSelectedModCopyForServerButton;UpdateSelectedModEditConfigButton;UpdateSelectedModFixButton
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### UpdateSelectedModCopyForServerButton

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** SelectedModCopyForServerButton
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ServerCommandBuilder

### UpdateSelectedModEditConfigButton

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** SelectedModEditConfigButton
- **MainWindow methods called:** UpdateSelectedModButton
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### UpdateSelectedModFixButton

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _isModUpdateInProgress
- **Fields written:** (none)
- **Untracked MainWindow members used:** SelectedModFixButton
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.ModBrowserInstallation.cs | InstallModFromBrowserAsync | UpdateSelectedModButtons |
| MainWindow.ModConfigScanning.cs | ScanForModConfigFilesAsync | UpdateSelectedModEditConfigButton |
| MainWindow.ModConfiguration.cs | EditConfigButton_OnClick | UpdateSelectedModEditConfigButton |
| MainWindow.ModDependencyRepair.cs | FixModButton_OnClick | UpdateSelectedModButtons |
| MainWindow.ModGridInput.cs | ModDatabaseCard_OnPreviewMouseLeftButtonDown | HandleModRowSelection |
| MainWindow.ModGridInput.cs | ModsDataGridRow_OnPreviewMouseLeftButtonDown | HandleModRowSelection |
| MainWindow.ModGridInput.cs | ModsDataGrid_OnPreviewMouseLeftButtonDown | ClearSelection |
| MainWindow.ModGridInput.cs | TryHandleModListKeyDownAsync | SelectAllModsInCurrentView |
| MainWindow.ModGridViewState.cs | AttachToModsView | ClearSelection |
| MainWindow.ModGridViewState.cs | ModsView_OnCollectionChanged | ClearSelection |
| MainWindow.ModInstallation.cs | InstallModButton_OnClick | RemoveFromSelection |
| MainWindow.ModInstallation.cs | InstallModButton_OnClick | UpdateSelectedModButtons |
| MainWindow.ModlistLoading.cs | PrepareForModlistLoad | ClearSelection |
| MainWindow.ModRefresh.cs | RefreshModsAsync | RestoreSelectionFromSourcePaths |
| MainWindow.ModUpdateCommands.cs | UpdateModsAsync | UpdateSelectedModButtons |
| MainWindow.PresetConfigurationImport.cs | ImportPresetConfigsAsync | UpdateSelectedModButtons |
| MainWindow.ServerSync.cs | UpdateServerOptionsState | UpdateSelectedModCopyForServerButton |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

(none)

## Field Ownership Notes

**Used only by this focused partial:**

- _modSelection

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _isApplyingPreset
- _isModUpdateInProgress
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (1)

- **UpdateSelectedModButton** — fields: (none) — touches no tracked MainWindow fields, generated members, or MainWindow methods; a pure function of its parameters, safe to move as a static helper.

### possible service extraction (4)

- **AddToSelection** — fields: _modSelection — touches field(s) owned mostly by this partial (_modSelection); candidate for a scoped service once similar methods are grouped.
- **ClearModDatabaseSelections** — fields: _modSelection — touches field(s) owned mostly by this partial (_modSelection); candidate for a scoped service once similar methods are grouped.
- **ClearSelection** — fields: _modSelection — touches field(s) owned mostly by this partial (_modSelection); candidate for a scoped service once similar methods are grouped.
- **RemoveFromSelection** — fields: _modSelection — touches field(s) owned mostly by this partial (_modSelection); candidate for a scoped service once similar methods are grouped.

### keep in MainWindow because UI/XAML-bound (1)

- **CloseModInfoButton_OnClick** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (4)

- **UpdateSelectedModButtons** — fields: _viewModel — references untracked MainWindow member(s) (SelectedModDatabasePageButton;SelectedModDeleteButton;SelectedModUpdateButton;_selectedMods) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **UpdateSelectedModCopyForServerButton** — fields: _userConfiguration — references untracked MainWindow member(s) (SelectedModCopyForServerButton) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **UpdateSelectedModEditConfigButton** — fields: _userConfiguration — references untracked MainWindow member(s) (SelectedModEditConfigButton) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **UpdateSelectedModFixButton** — fields: _isModUpdateInProgress — references untracked MainWindow member(s) (SelectedModFixButton) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.

### high-risk due to shared state (4)

- **GetModsInViewOrder** — fields: _viewModel — touches field(s) shared broadly across MainWindow (_viewModel); moving it risks splitting shared state across files.
- **HandleModRowSelection** — fields: _isApplyingPreset;_modSelection — touches field(s) shared broadly across MainWindow (_isApplyingPreset;_modSelection); moving it risks splitting shared state across files.
- **RestoreSelectionFromSourcePaths** — fields: _modSelection;_viewModel — touches field(s) shared broadly across MainWindow (_modSelection;_viewModel); moving it risks splitting shared state across files.
- **SelectAllModsInCurrentView** — fields: _isApplyingPreset;_modSelection — touches field(s) shared broadly across MainWindow (_isApplyingPreset;_modSelection); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 4 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
