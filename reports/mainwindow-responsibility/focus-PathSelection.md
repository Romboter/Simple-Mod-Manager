# Focused Report: MainWindow.PathSelection.cs

## Header

- **Focused partial file:** MainWindow.PathSelection.cs
- **Line count:** 150
- **Method count:** 5
- **Event handler count:** 2

## Method Inventory

### GetInitialConfigDirectory

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

### PromptForConfigFile

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** GetInitialConfigDirectory
- **Awaited calls:** (none)
- **External types used:** (none)

### PromptForDirectory

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox

### SelectDataFolderMenuItem_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** MenuItem.Click
- **Fields read:** _dataDirectory;_userConfiguration
- **Fields written:** _dataDirectory
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** PromptForDirectory;RefreshDeveloperProfilesMenuEntries;ReloadViewModelAsync
- **Awaited calls:** ReloadViewModelAsync
- **External types used:** VintageStoryModManager.Services.DeveloperProfileManager;VintageStoryModManager.Services.InstallationPathValidator

### SelectGameFolderMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** MenuItem.Click
- **Fields read:** _gameDirectory;_userConfiguration
- **Fields written:** _gameDirectory
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** PromptForDirectory;UpdateGameVersionMenuItem
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.InstallationPathValidator;VintageStoryModManager.Services.VintageStoryVersionLocator

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.ModConfiguration.cs | EditConfigButton_OnClick | PromptForConfigFile |
| MainWindow.PathInitialization.cs | TryResolveDataDirectory | PromptForDirectory |
| MainWindow.PathInitialization.cs | TryResolveGameDirectory | PromptForDirectory |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| SelectDataFolderMenuItem_OnClick | MainWindow.DeveloperProfiles.cs | RefreshDeveloperProfilesMenuEntries |
| SelectDataFolderMenuItem_OnClick | MainWindow.ViewModel.cs | ReloadViewModelAsync |
| SelectGameFolderMenuItem_OnClick | MainWindow.Settings.cs | UpdateGameVersionMenuItem |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory
- _gameDirectory
- _userConfiguration

## Extraction Seam Suggestions

### safe helper extraction (1)

- **PromptForDirectory** — fields: (none) — touches no tracked MainWindow fields, generated members, or MainWindow methods; a pure function of its parameters, safe to move as a static helper.

### possible service extraction (1)

- **PromptForConfigFile** — fields: (none) — touches field(s) owned mostly by this partial ((none)); candidate for a scoped service once similar methods are grouped.

### keep in MainWindow because UI/XAML-bound (2)

- **SelectDataFolderMenuItem_OnClick** — fields: _dataDirectory;_userConfiguration — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **SelectGameFolderMenuItem_OnClick** — fields: _gameDirectory;_userConfiguration — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (1)

- **GetInitialConfigDirectory** — fields: _dataDirectory — touches field(s) shared broadly across MainWindow (_dataDirectory); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
