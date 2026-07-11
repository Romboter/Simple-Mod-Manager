# Focused Report: MainWindow.ModInstallation.cs

## Header

- **Focused partial file:** MainWindow.ModInstallation.cs
- **Line count:** 119
- **Method count:** 1
- **Event handler count:** 1

## Method Inventory

### InstallModButton_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel
- **Fields written:** _isModUpdateInProgress
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateAutomaticBackupAsync;RefreshModsAsync;RemoveFromSelection;UpdateSelectedModButtons
- **Awaited calls:** CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait;ModUpdateOperationHelper.ExecuteAsync(
                    _modUpdateService, descriptor, _userConfiguration.CacheAllVersionsLocally, progress,
                    "The installation failed.")
                .ConfigureAwait;RefreshModsAsync().ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.Services.ModInstallTargetPathHelper;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.ModReleaseSelectionHelper;VintageStoryModManager.Services.ModUpdateDescriptor;VintageStoryModManager.Services.ModUpdateOperationHelper;VintageStoryModManager.Services.ModUpdateOperationOutcome;VintageStoryModManager.Services.ModUpdateProgress;VintageStoryModManager.ViewModels.ModListItemViewModel

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

(none)

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| InstallModButton_OnClick | MainWindow.ModGridSelection.cs | RemoveFromSelection |
| InstallModButton_OnClick | MainWindow.ModGridSelection.cs | UpdateSelectedModButtons |
| InstallModButton_OnClick | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| InstallModButton_OnClick | MainWindow.ModRefresh.cs | RefreshModsAsync |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory
- _isModUpdateInProgress
- _modActivityLoggingService
- _modUpdateService
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (1)

- **InstallModButton_OnClick** — fields: _dataDirectory;_isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (0)

(none)

## Risk Summary

- **Risk level:** low
- **Reasons:** no methods touch broadly-shared fields or UI/generated members, and none are plausible service candidates beyond safe helpers; this partial looks low-risk to extract from.
