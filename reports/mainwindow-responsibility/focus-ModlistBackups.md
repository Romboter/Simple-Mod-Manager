# Focused Report: MainWindow.ModlistBackups.cs

## Header

- **Focused partial file:** MainWindow.ModlistBackups.cs
- **Line count:** 342
- **Method count:** 6
- **Event handler count:** 2

## Method Inventory

### CaptureConfigurationsForBackup

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModConfigurationCaptureError;VintageStoryModManager.Services.ModConfigurationCaptureRequest;VintageStoryModManager.Services.ModConfigurationCaptureResult;VintageStoryModManager.Services.ModConfigurationCaptureService;VintageStoryModManager.ViewModels.ModListItemViewModel

### CreateAppStartedBackupAsync

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateBackupAsync
- **Awaited calls:** (none)
- **External types used:** (none)

### CreateBackupAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _backupSemaphore;_viewModel
- **Fields written:** _viewModel
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CaptureConfigurationsForBackup;EnsureBackupDirectory;ResolveGameVersion
- **Awaited calls:** File.WriteAllTextAsync(filePath, json).ConfigureAwait;_backupSemaphore.WaitAsync().ConfigureAwait
- **External types used:** VintageStoryModManager.Helpers.FileNameHelper;VintageStoryModManager.Models.SerializablePreset;VintageStoryModManager.Services.BackupRetentionService;VintageStoryModManager.Services.PdfModlistSerializer;VintageStoryModManager.Services.PresetSnapshotBuilder

### RestoreBackupAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** ModListLoadOptions;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ApplyPresetAsync
- **Awaited calls:** ApplyPresetAsync(loadedPreset, restoreConfigurations).ConfigureAwait
- **External types used:** VintageStoryModManager.Models.ModPreset;VintageStoryModManager.Models.ModPreset?;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.PresetFileLoader

### RestoreBackupMenuItem_OnBackupClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** RestoreBackupAsync
- **Awaited calls:** RestoreBackupAsync(filePath, confirmationDialog.RestoreConfigurations).ConfigureAwait
- **External types used:** VintageStoryModManager.Views.Dialogs.RestoreBackupDialog

### RestoreBackupMenuItem_OnSubmenuOpened

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** MenuItem.SubmenuOpened
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** EnsureBackupDirectory
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.BackupRetentionService

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync | CreateBackupAsync |
| MainWindow.Startup.cs | MainWindow_Loaded | CreateAppStartedBackupAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| CreateBackupAsync | MainWindow.FeatureDirectories.cs | EnsureBackupDirectory |
| CreateBackupAsync | MainWindow.PresetFiles.cs | ResolveGameVersion |
| RestoreBackupAsync | MainWindow.PresetApplication.cs | ApplyPresetAsync |
| RestoreBackupMenuItem_OnSubmenuOpened | MainWindow.FeatureDirectories.cs | EnsureBackupDirectory |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

- ModListLoadOptions
- _backupSemaphore

**Shared broadly across MainWindow:**

- _dataDirectory
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (1)

- **CreateAppStartedBackupAsync** — fields: (none) — touches field(s) owned mostly by this partial ((none)); candidate for a scoped service once similar methods are grouped.

### keep in MainWindow because UI/XAML-bound (2)

- **RestoreBackupMenuItem_OnBackupClick** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **RestoreBackupMenuItem_OnSubmenuOpened** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (3)

- **CaptureConfigurationsForBackup** — fields: _dataDirectory;_userConfiguration — touches field(s) shared broadly across MainWindow (_dataDirectory;_userConfiguration); moving it risks splitting shared state across files.
- **CreateBackupAsync** — fields: _backupSemaphore;_viewModel — touches field(s) shared broadly across MainWindow (_backupSemaphore;_viewModel); moving it risks splitting shared state across files.
- **RestoreBackupAsync** — fields: ModListLoadOptions;_viewModel — touches field(s) shared broadly across MainWindow (ModListLoadOptions;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 3 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
