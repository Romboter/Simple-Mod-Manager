# Focused Report: MainWindow.ModDeletion.cs

## Header

- **Focused partial file:** MainWindow.ModDeletion.cs
- **Line count:** 204
- **Method count:** 5
- **Event handler count:** 1

## Method Inventory

### DeleteModButton_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** SelectedModDeleteButton.Click
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** _selectedMods
- **MainWindow methods called:** DeleteSelectedModsAsync;DeleteSingleModAsync
- **Awaited calls:** DeleteSelectedModsAsync;DeleteSingleModAsync
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### DeleteMultipleModsAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateAutomaticBackupAsync;RefreshModsAsync;TryDeleteModAtPath;TryGetManagedModPath
- **Awaited calls:** CreateAutomaticBackupAsync("ModsDeleted").ConfigureAwait;RefreshModsAsync
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.ViewModels.ModListItemViewModel

### DeleteSelectedModsAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** _selectedMods
- **MainWindow methods called:** DeleteMultipleModsAsync;DeleteSingleModAsync
- **Awaited calls:** DeleteMultipleModsAsync;DeleteSingleModAsync
- **External types used:** (none)

### DeleteSingleModAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateAutomaticBackupAsync;RefreshModsAsync;TryDeleteModAtPath;TryGetManagedModPath
- **Awaited calls:** CreateAutomaticBackupAsync("ModsDeleted").ConfigureAwait;RefreshModsAsync
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox

### TryDeleteModAtPath

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _modActivityLoggingService;_userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.ModGridInput.cs | TryHandleModListKeyDownAsync | DeleteSelectedModsAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| DeleteMultipleModsAsync | MainWindow.ManagedModPaths.cs | TryGetManagedModPath |
| DeleteMultipleModsAsync | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| DeleteMultipleModsAsync | MainWindow.ModRefresh.cs | RefreshModsAsync |
| DeleteSingleModAsync | MainWindow.ManagedModPaths.cs | TryGetManagedModPath |
| DeleteSingleModAsync | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| DeleteSingleModAsync | MainWindow.ModRefresh.cs | RefreshModsAsync |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _modActivityLoggingService
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (1)

- **DeleteModButton_OnClick** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (1)

- **DeleteSelectedModsAsync** — fields: (none) — references untracked MainWindow member(s) (_selectedMods) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.

### high-risk due to shared state (3)

- **DeleteMultipleModsAsync** — fields: _viewModel — touches field(s) shared broadly across MainWindow (_viewModel); moving it risks splitting shared state across files.
- **DeleteSingleModAsync** — fields: _viewModel — touches field(s) shared broadly across MainWindow (_viewModel); moving it risks splitting shared state across files.
- **TryDeleteModAtPath** — fields: _modActivityLoggingService;_userConfiguration — touches field(s) shared broadly across MainWindow (_modActivityLoggingService;_userConfiguration); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 3 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
