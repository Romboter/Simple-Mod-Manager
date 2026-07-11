# Focused Report: MainWindow.PresetVersionApplication.cs

## Header

- **Focused partial file:** MainWindow.PresetVersionApplication.cs
- **Line count:** 319
- **Method count:** 2
- **Event handler count:** 0

## Method Inventory

### ApplyPresetModVersionsAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _recentLocalModBackupDirectory;_recentLocalModBackupModNames;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** BeginModlistInstallUi;CompleteModlistInstallStep;CreateModlistInstallProgressReporter;EndModlistInstallUi;TryInstallPresetModAsync;UpdateModsAsync
- **Awaited calls:** TryInstallPresetModAsync(candidate, progress).ConfigureAwait;UpdateModsAsync(overrides.Keys.ToList(), true, overrides, false, progressFactory,
                        completionCallback)
                    .ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModPresetModState;VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.PresetModInstallResult;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.ModUpdateProgress;VintageStoryModManager.Services.ModUpdateResult;VintageStoryModManager.Services.VersionStringUtility;VintageStoryModManager.ViewModels.ModListItemViewModel;VintageStoryModManager.ViewModels.ModListItemViewModel?;VintageStoryModManager.ViewModels.ModVersionOptionViewModel

### TryInstallPresetModAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_modActivityLoggingService;_modDatabaseService;_modUpdateService;_userConfiguration;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateModUpdateProgressReporter
- **Awaited calls:** ModUpdateOperationHelper.ExecuteAsync(
                _modUpdateService, descriptor, _userConfiguration.CacheAllVersionsLocally, progress,
                "The installation failed.")
            .ConfigureAwait;_modDatabaseService
            .TryLoadDatabaseInfoAsync(modId, desiredVersion, _viewModel.InstalledGameVersion,
                _userConfiguration.RequireExactVsVersionMatch)
            .ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModDatabaseInfo;VintageStoryModManager.Models.ModReleaseInfo;VintageStoryModManager.PresetModInstallResult;VintageStoryModManager.Services.ModInstallTargetPathHelper;VintageStoryModManager.Services.ModUpdateDescriptor;VintageStoryModManager.Services.ModUpdateOperationHelper;VintageStoryModManager.Services.ModUpdateOperationOutcome;VintageStoryModManager.Services.VersionStringUtility

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.PresetApplication.cs | ApplyPresetAsync | ApplyPresetModVersionsAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| ApplyPresetModVersionsAsync | MainWindow.ModUpdateCommands.cs | UpdateModsAsync |
| ApplyPresetModVersionsAsync | MainWindow.Progress.cs | BeginModlistInstallUi |
| ApplyPresetModVersionsAsync | MainWindow.Progress.cs | CompleteModlistInstallStep |
| ApplyPresetModVersionsAsync | MainWindow.Progress.cs | CreateModlistInstallProgressReporter |
| ApplyPresetModVersionsAsync | MainWindow.Progress.cs | EndModlistInstallUi |
| TryInstallPresetModAsync | MainWindow.Progress.cs | CreateModUpdateProgressReporter |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory
- _modActivityLoggingService
- _modDatabaseService
- _modUpdateService
- _recentLocalModBackupDirectory
- _recentLocalModBackupModNames
- _userConfiguration
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

### high-risk due to shared state (2)

- **ApplyPresetModVersionsAsync** — fields: _recentLocalModBackupDirectory;_recentLocalModBackupModNames;_viewModel — touches field(s) shared broadly across MainWindow (_recentLocalModBackupDirectory;_recentLocalModBackupModNames;_viewModel); moving it risks splitting shared state across files.
- **TryInstallPresetModAsync** — fields: _dataDirectory;_modActivityLoggingService;_modDatabaseService;_modUpdateService;_userConfiguration;_viewModel — touches field(s) shared broadly across MainWindow (_dataDirectory;_modActivityLoggingService;_modDatabaseService;_modUpdateService;_userConfiguration;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 2 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
