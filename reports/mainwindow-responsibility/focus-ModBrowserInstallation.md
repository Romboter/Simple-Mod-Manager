# Focused Report: MainWindow.ModBrowserInstallation.cs

## Header

- **Focused partial file:** MainWindow.ModBrowserInstallation.cs
- **Line count:** 243
- **Method count:** 4
- **Event handler count:** 0

## Method Inventory

### AddModToInstalledAndRemoveFromSearch

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modBrowserViewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### ConvertToModListItemViewModel

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Models.ModDatabaseInfo;VintageStoryModManager.Models.ModEntry;VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.Models.ModSourceKind;VintageStoryModManager.ViewModels.ActivationResult;VintageStoryModManager.ViewModels.ModListItemViewModel

### ConvertToModReleaseInfo

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Models.ModReleaseInfo

### InstallModFromBrowserAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel
- **Fields written:** _isModUpdateInProgress
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** AddModToInstalledAndRemoveFromSearch;ConvertToModListItemViewModel;CreateAutomaticBackupAsync;RefreshModsAsync;UpdateSelectedModButtons
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
| InstallModFromBrowserAsync | MainWindow.ModGridSelection.cs | UpdateSelectedModButtons |
| InstallModFromBrowserAsync | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| InstallModFromBrowserAsync | MainWindow.ModRefresh.cs | RefreshModsAsync |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

- _modUpdateService

**Shared broadly across MainWindow:**

- _dataDirectory
- _isModUpdateInProgress
- _modActivityLoggingService
- _modBrowserViewModel
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (1)

- **ConvertToModReleaseInfo** — fields: (none) — touches no tracked MainWindow fields, generated members, or MainWindow methods; a pure function of its parameters, safe to move as a static helper.

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (0)

(none)

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (3)

- **AddModToInstalledAndRemoveFromSearch** — fields: _modBrowserViewModel — touches field(s) shared broadly across MainWindow (_modBrowserViewModel); moving it risks splitting shared state across files.
- **ConvertToModListItemViewModel** — fields: _viewModel — touches field(s) shared broadly across MainWindow (_viewModel); moving it risks splitting shared state across files.
- **InstallModFromBrowserAsync** — fields: _dataDirectory;_isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel — touches field(s) shared broadly across MainWindow (_dataDirectory;_isModUpdateInProgress;_modActivityLoggingService;_modUpdateService;_userConfiguration;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 3 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
