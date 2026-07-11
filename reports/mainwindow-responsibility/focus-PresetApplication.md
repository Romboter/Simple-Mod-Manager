# Focused Report: MainWindow.PresetApplication.cs

## Header

- **Focused partial file:** MainWindow.PresetApplication.cs
- **Line count:** 52
- **Method count:** 1
- **Event handler count:** 0

## Method Inventory

### ApplyPresetAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _isApplyingPreset;_viewModel
- **Fields written:** _isApplyingPreset;_recentLocalModBackupDirectory;_recentLocalModBackupModNames;_refreshAfterModlistLoadPending
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ApplyExclusivePresetAsync;ApplyPresetModVersionsAsync;ImportPresetConfigsAsync;ScheduleRefreshAfterModlistLoadIfReady;UpdateModlistLoadingUiState
- **Awaited calls:** ApplyExclusivePresetAsync(preset).ConfigureAwait;ApplyPresetModVersionsAsync(preset).ConfigureAwait;ImportPresetConfigsAsync(preset).ConfigureAwait;viewModel.ApplyPresetAsync(preset).ConfigureAwait
- **External types used:** VintageStoryModManager.ViewModels.MainViewModel

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.CloudLoad.cs | InstallCloudModlistButton_OnClick | ApplyPresetAsync |
| MainWindow.CloudLoad.cs | LoadModlistFromCloudMenuItem_OnClick | ApplyPresetAsync |
| MainWindow.ModlistBackups.cs | RestoreBackupAsync | ApplyPresetAsync |
| MainWindow.ModlistLoading.cs | LoadModlistFromFileAsync | ApplyPresetAsync |
| MainWindow.PresetFiles.cs | LoadPresetFromFileAsync | ApplyPresetAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| ApplyPresetAsync | MainWindow.ExclusivePresetApplication.cs | ApplyExclusivePresetAsync |
| ApplyPresetAsync | MainWindow.ModlistLoading.cs | UpdateModlistLoadingUiState |
| ApplyPresetAsync | MainWindow.ModRefresh.cs | ScheduleRefreshAfterModlistLoadIfReady |
| ApplyPresetAsync | MainWindow.PresetConfigurationImport.cs | ImportPresetConfigsAsync |
| ApplyPresetAsync | MainWindow.PresetVersionApplication.cs | ApplyPresetModVersionsAsync |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _isApplyingPreset
- _recentLocalModBackupDirectory
- _recentLocalModBackupModNames
- _refreshAfterModlistLoadPending
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (0)

(none)

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (1)

- **ApplyPresetAsync** — fields: _isApplyingPreset;_recentLocalModBackupDirectory;_recentLocalModBackupModNames;_refreshAfterModlistLoadPending;_viewModel — touches field(s) shared broadly across MainWindow (_isApplyingPreset;_recentLocalModBackupDirectory;_recentLocalModBackupModNames;_refreshAfterModlistLoadPending;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
