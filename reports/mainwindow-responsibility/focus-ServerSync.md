# Focused Report: MainWindow.ServerSync.cs

## Header

- **Focused partial file:** MainWindow.ServerSync.cs
- **Line count:** 110
- **Method count:** 5
- **Event handler count:** 3

## Method Inventory

### EnableServerOptionsMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** EnableServerOptionsMenuItem.Click
- **Fields read:** _userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** UpdateServerOptionsState
- **Awaited calls:** (none)
- **External types used:** (none)

### ManageServerTargetsMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** ManageServerTargetsMenuItem.Click
- **Fields read:** _serverTargetService
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** UpdateSyncToServerMenuState
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Views.Dialogs.ManageServerTargetsDialog

### SyncToServerMenuItem_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** SyncToServerMenuItem.Click
- **Fields read:** _dataDirectory;_serverTargetService;_syncEngine;_userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** CreateHostKeyVerifierWithStorage
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Models.ServerTarget;VintageStoryModManager.Services.ConfirmationService;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.ViewModels.SyncToServerDialogViewModel;VintageStoryModManager.Views.Dialogs.SyncToServerDialog

### UpdateServerOptionsState

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** EnableServerOptionsMenuItem;ManageServerTargetsMenuItem;ServerOptionsSeparator1;ServerOptionsSeparator2;SyncToServerMenuItem;_selectedMods
- **MainWindow methods called:** UpdateSelectedModCopyForServerButton
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### UpdateSyncToServerMenuState

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** SyncToServerMenuItem
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** (none)

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.Profiles.cs | EditGameProfileMenuItem_OnClick | UpdateSyncToServerMenuState |
| MainWindow.Profiles.cs | OnActiveGameProfileChangedAsync | UpdateSyncToServerMenuState |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| SyncToServerMenuItem_OnClick | MainWindow.ServerConnection.cs | CreateHostKeyVerifierWithStorage |
| UpdateServerOptionsState | MainWindow.ModGridSelection.cs | UpdateSelectedModCopyForServerButton |

## Field Ownership Notes

**Used only by this focused partial:**

- _syncEngine

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory
- _serverTargetService
- _userConfiguration

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (3)

- **EnableServerOptionsMenuItem_OnClick** — fields: _userConfiguration — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **ManageServerTargetsMenuItem_OnClick** — fields: _serverTargetService — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **SyncToServerMenuItem_OnClick** — fields: _dataDirectory;_serverTargetService;_syncEngine;_userConfiguration — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (2)

- **UpdateServerOptionsState** — fields: (none) — references untracked MainWindow member(s) (EnableServerOptionsMenuItem;ManageServerTargetsMenuItem;ServerOptionsSeparator1;ServerOptionsSeparator2;SyncToServerMenuItem;_selectedMods) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **UpdateSyncToServerMenuState** — fields: _userConfiguration — references untracked MainWindow member(s) (SyncToServerMenuItem) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.

### high-risk due to shared state (0)

(none)

## Risk Summary

- **Risk level:** medium
- **Reasons:** 0 method(s) are plausible service candidates and 2 method(s) touch UI/generated MainWindow members; a scoped extraction pass is warranted before moving anything.
