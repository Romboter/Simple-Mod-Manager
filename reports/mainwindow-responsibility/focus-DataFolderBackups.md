# Focused Report: MainWindow.DataFolderBackups.cs

## Header

- **Focused partial file:** MainWindow.DataFolderBackups.cs
- **Line count:** 452
- **Method count:** 9
- **Event handler count:** 5

## Method Inventory

### ChangeBackupLocationMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _dataFolderBackupCoordinator;_userConfiguration;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox

### CreateDataBackupProgressReporter

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** DataBackupProgress;DataBackupStatusMessage
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.DataBackupProgress

### DeleteDataFolderBackupsMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_dataFolderBackupCoordinator;_gameDirectory;_viewModel
- **Fields written:** _dataDirectory
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.VersionStringUtility;VintageStoryModManager.Services.VintageStoryVersionLocator

### HideDataBackupOverlay

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** DataBackupProgress;DataBackupStatusMessage;IsDataBackupInProgress
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### OpenDataBackupDirectoryMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _dataFolderBackupCoordinator
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox

### RestoreDataBackupAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_dataFolderBackupCoordinator;_gameDirectory;_viewModel
- **Fields written:** _dataDirectory
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateDataBackupProgressReporter;HideDataBackupOverlay;RefreshModsAsync;ShowDataBackupOverlay
- **Awaited calls:** RefreshModsAsync(true).ConfigureAwait;_dataFolderBackupCoordinator.RestoreBackupAsync(summary, _dataDirectory!, progress, CancellationToken.None)
                .ConfigureAwait
- **External types used:** VintageStoryModManager.Helpers.PathRelationshipHelper;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.VersionStringUtility;VintageStoryModManager.Services.VintageStoryVersionLocator

### RestoreDataFolderMenuItem_OnBackupClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _dataDirectory
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** RestoreDataBackupAsync
- **Awaited calls:** RestoreDataBackupAsync(summary).ConfigureAwait
- **External types used:** VintageStoryModManager.Services.DataBackupSummary;VintageStoryModManager.Services.ModManagerMessageBox

### RestoreDataFolderMenuItem_OnSubmenuOpened

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** RestoreDataFolderMenuItem.SubmenuOpened
- **Fields read:** MaxDataBackupsMenuItems;_dataDirectory;_dataFolderBackupCoordinator;_gameDirectory
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Helpers.PathRelationshipHelper;VintageStoryModManager.Services.DataBackupSummary;VintageStoryModManager.Services.VersionStringUtility;VintageStoryModManager.Services.VintageStoryVersionLocator

### ShowDataBackupOverlay

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** DataBackupProgress;DataBackupStatusMessage;IsDataBackupInProgress
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.GameLaunch.cs | TryEnsureDataBackupBeforeLaunchAsync | CreateDataBackupProgressReporter |
| MainWindow.GameLaunch.cs | TryEnsureDataBackupBeforeLaunchAsync | HideDataBackupOverlay |
| MainWindow.GameLaunch.cs | TryEnsureDataBackupBeforeLaunchAsync | ShowDataBackupOverlay |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| RestoreDataBackupAsync | MainWindow.ModRefresh.cs | RefreshModsAsync |

## Field Ownership Notes

**Used only by this focused partial:**

- MaxDataBackupsMenuItems

**Used mostly by this focused partial:**

- _dataDirectory
- _dataFolderBackupCoordinator

**Shared broadly across MainWindow:**

- _gameDirectory
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (5)

- **ChangeBackupLocationMenuItem_OnClick** — fields: _dataFolderBackupCoordinator;_userConfiguration;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **DeleteDataFolderBackupsMenuItem_OnClick** — fields: _dataDirectory;_dataFolderBackupCoordinator;_gameDirectory;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **OpenDataBackupDirectoryMenuItem_OnClick** — fields: _dataFolderBackupCoordinator — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **RestoreDataFolderMenuItem_OnBackupClick** — fields: _dataDirectory — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **RestoreDataFolderMenuItem_OnSubmenuOpened** — fields: MaxDataBackupsMenuItems;_dataDirectory;_dataFolderBackupCoordinator;_gameDirectory — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (3)

- **CreateDataBackupProgressReporter** — fields: (none) — references untracked MainWindow member(s) (DataBackupProgress;DataBackupStatusMessage) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **HideDataBackupOverlay** — fields: (none) — references untracked MainWindow member(s) (DataBackupProgress;DataBackupStatusMessage;IsDataBackupInProgress) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **ShowDataBackupOverlay** — fields: (none) — references untracked MainWindow member(s) (DataBackupProgress;DataBackupStatusMessage;IsDataBackupInProgress) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.

### high-risk due to shared state (1)

- **RestoreDataBackupAsync** — fields: _dataDirectory;_dataFolderBackupCoordinator;_gameDirectory;_viewModel — touches field(s) shared broadly across MainWindow (_dataDirectory;_dataFolderBackupCoordinator;_gameDirectory;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
