# Focused Report: MainWindow.ModDependencyRepair.cs

## Header

- **Focused partial file:** MainWindow.ModDependencyRepair.cs
- **Line count:** 224
- **Method count:** 2
- **Event handler count:** 1

## Method Inventory

### FixModButton_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** SelectedModFixButton.Click
- **Fields read:** _isModUpdateInProgress;_viewModel
- **Fields written:** _isModUpdateInProgress
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** InstallOrUpdateDependencyAsync;UpdateSelectedModButtons
- **Awaited calls:** InstallOrUpdateDependencyAsync(dependency, installedDependency)
                        .ConfigureAwait;viewModel.RefreshModsWithErrorsAsync(modsToRefresh).ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModDependencyInfo;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.VersionStringUtility;VintageStoryModManager.ViewModels.ModListItemViewModel

### InstallOrUpdateDependencyAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_modDatabaseService;_modUpdateService;_userConfiguration;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** TryGetManagedModPath
- **Awaited calls:** ModUpdateOperationHelper.ExecuteAsync(
                    _modUpdateService, descriptor, _userConfiguration.CacheAllVersionsLocally, progress,
                    "The installation failed.")
                .ConfigureAwait;_modDatabaseService
                .TryLoadDatabaseInfoAsync(dependency.ModId, installedMod?.Version, _viewModel?.InstalledGameVersion,
                    _userConfiguration.RequireExactVsVersionMatch)
                .ConfigureAwait;_viewModel.PreserveActivationStateAsync(
                    dependency.ModId,
                    installedMod.Version,
                    release.Version,
                    wasActive).ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModDatabaseInfo;VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.Services.ModInstallTargetPathHelper;VintageStoryModManager.Services.ModReleaseSelectionHelper;VintageStoryModManager.Services.ModUpdateDescriptor;VintageStoryModManager.Services.ModUpdateOperationHelper;VintageStoryModManager.Services.ModUpdateOperationOutcome;VintageStoryModManager.Services.ModUpdateProgress;VintageStoryModManager.Services.ModUpdateTargetPathHelper

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

(none)

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| FixModButton_OnClick | MainWindow.ModGridSelection.cs | UpdateSelectedModButtons |
| InstallOrUpdateDependencyAsync | MainWindow.ManagedModPaths.cs | TryGetManagedModPath |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory
- _isModUpdateInProgress
- _modDatabaseService
- _modUpdateService
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (1)

- **FixModButton_OnClick** — fields: _isModUpdateInProgress;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (1)

- **InstallOrUpdateDependencyAsync** — fields: _dataDirectory;_modDatabaseService;_modUpdateService;_userConfiguration;_viewModel — touches field(s) shared broadly across MainWindow (_dataDirectory;_modDatabaseService;_modUpdateService;_userConfiguration;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
