# Focused Report: MainWindow.ModUpdateCommands.cs

## Header

- **Focused partial file:** MainWindow.ModUpdateCommands.cs
- **Line count:** 327
- **Method count:** 5
- **Event handler count:** 4

## Method Inventory

### SelectedModVersionComboBox_OnDropDownOpened

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** SelectedModVersionComboBox.DropDownOpened
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** FindDescendantScrollViewer
- **Awaited calls:** (none)
- **External types used:** (none)

### SelectedModVersionComboBox_OnSelectionChanged

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** SelectedModVersionComboBox.SelectionChanged
- **Fields read:** _isModUpdateInProgress;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** UpdateModsAsync
- **Awaited calls:** UpdateModsAsync
- **External types used:** VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.ViewModels.ModListItemViewModel;VintageStoryModManager.ViewModels.ModVersionOptionViewModel

### UpdateAllModsMenuItem_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** UpdateAllButton.Click
- **Fields read:** _isApplyingPreset;_isModUpdateInProgress;_userConfiguration;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateAutomaticBackupAsync;UpdateModsAsync
- **Awaited calls:** CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait;UpdateModsAsync(selectedMods, true, selectedOverrides).ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.Models.ModReleaseInfo?;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.ViewModels.ModListItemViewModel;VintageStoryModManager.Views.Dialogs.UpdateModsDialog

### UpdateModButton_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** SelectedModUpdateButton.Click
- **Fields read:** _isModUpdateInProgress
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** UpdateModsAsync
- **Awaited calls:** UpdateModsAsync
- **External types used:** VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.ViewModels.ModListItemViewModel

### UpdateModsAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel
- **Fields written:** _isModUpdateInProgress
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** BeginModlistInstallUi;CompleteModlistInstallStep;CreateModUpdateProgressReporter;CreateModlistInstallProgressReporter;EndModlistInstallUi;RefreshDeleteCachedModsMenuHeaderAsync;RefreshModsAsync;TryGetManagedModPath;UpdateSelectedModButtons
- **Awaited calls:** RefreshDeleteCachedModsMenuHeaderAsync;RefreshModsAsync().ConfigureAwait;_modUpdateService
                    .UpdateAsync(descriptor, _userConfiguration.CacheAllVersionsLocally, progress)
                    .ConfigureAwait;_viewModel.PreserveActivationStateAsync(mod.ModId ?? string.Empty, mod.Version, release.Version, mod.IsActive)
                    .ConfigureAwait
- **External types used:** VintageStoryModManager.ModUpdateOperationResult;VintageStoryModManager.ModUpdateReleasePreference;VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.Services.ModChangelogFormatter;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.ModReleaseSelectionHelper;VintageStoryModManager.Services.ModUpdateDescriptor;VintageStoryModManager.Services.ModUpdateDialogHelper;VintageStoryModManager.Services.ModUpdateProgress;VintageStoryModManager.Services.ModUpdateResult;VintageStoryModManager.Services.ModUpdateTargetPathHelper;VintageStoryModManager.ViewModels.ModListItemViewModel

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.PresetVersionApplication.cs | ApplyPresetModVersionsAsync | UpdateModsAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| SelectedModVersionComboBox_OnDropDownOpened | MainWindow.ModGridScrolling.cs | FindDescendantScrollViewer |
| UpdateAllModsMenuItem_OnClick | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| UpdateModsAsync | MainWindow.ManagedModPaths.cs | TryGetManagedModPath |
| UpdateModsAsync | MainWindow.ManagerCache.cs | RefreshDeleteCachedModsMenuHeaderAsync |
| UpdateModsAsync | MainWindow.ModGridSelection.cs | UpdateSelectedModButtons |
| UpdateModsAsync | MainWindow.ModRefresh.cs | RefreshModsAsync |
| UpdateModsAsync | MainWindow.Progress.cs | BeginModlistInstallUi |
| UpdateModsAsync | MainWindow.Progress.cs | CompleteModlistInstallStep |
| UpdateModsAsync | MainWindow.Progress.cs | CreateModUpdateProgressReporter |
| UpdateModsAsync | MainWindow.Progress.cs | CreateModlistInstallProgressReporter |
| UpdateModsAsync | MainWindow.Progress.cs | EndModlistInstallUi |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

- _isModUpdateInProgress

**Shared broadly across MainWindow:**

- _isApplyingPreset
- _modActivityLoggingService
- _modUpdateService
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (4)

- **SelectedModVersionComboBox_OnDropDownOpened** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **SelectedModVersionComboBox_OnSelectionChanged** — fields: _isModUpdateInProgress;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **UpdateAllModsMenuItem_OnClick** — fields: _isApplyingPreset;_isModUpdateInProgress;_userConfiguration;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **UpdateModButton_OnClick** — fields: _isModUpdateInProgress — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (1)

- **UpdateModsAsync** — fields: _isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel — touches field(s) shared broadly across MainWindow (_isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
