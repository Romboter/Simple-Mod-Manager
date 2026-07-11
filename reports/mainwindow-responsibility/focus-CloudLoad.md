# Focused Report: MainWindow.CloudLoad.cs

## Header

- **Focused partial file:** MainWindow.CloudLoad.cs
- **Line count:** 332
- **Method count:** 9
- **Event handler count:** 5

## Method Inventory

### CloudModlistsDataGrid_OnSelectionChanged

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** CloudModlistsDataGrid.SelectionChanged
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** CloudModlistsDataGrid
- **MainWindow methods called:** SetCloudModlistSelection
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Models.CloudModlistListEntry

### DeleteCloudModlistMenuItem_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** _cloudModlistsLoaded
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ExecuteCloudOperationAsync;RefreshCloudModlistsAsync
- **Awaited calls:** CloudModlistManagementService.DeleteSlotAsync;CloudModlistSlotService.LoadSlotsAsync;ExecuteCloudOperationAsync;RefreshCloudModlistsAsync
- **External types used:** VintageStoryModManager.Models.CloudModlistSlot;VintageStoryModManager.Services.CloudModlistHelper;VintageStoryModManager.Services.CloudModlistManagementService;VintageStoryModManager.Services.CloudModlistSlotService;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Views.Dialogs.CloudSlotSelectionDialog

### EnsureCloudModlistContentAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ExecuteCloudOperationAsync;SetCloudModlistSelection
- **Awaited calls:** CloudModlistContentService.EnsureContentAsync;ExecuteCloudOperationAsync
- **External types used:** VintageStoryModManager.Models.CloudModlistListEntry;VintageStoryModManager.Services.CloudModlistContentService;VintageStoryModManager.Services.ModManagerMessageBox

### InstallCloudModlistButton_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** InstallCloudModlistButton.Click
- **Fields read:** _selectedCloudModlist;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ApplyPresetAsync;CreateAutomaticBackupAsync;EnsureCloudModListCacheDirectory;EnsureCloudModlistContentAsync;EnsureModlistBackupBeforeLoad;GetModlistLoadOptions;PrepareForModlistLoad;PromptModlistLoadMode
- **Awaited calls:** ApplyPresetAsync;CloudModlistCacheService.CacheAsync;CreateAutomaticBackupAsync("ModlistLoaded").ConfigureAwait;EnsureCloudModlistContentAsync
- **External types used:** VintageStoryModManager.Models.CloudModlistListEntry;VintageStoryModManager.Models.ModPreset?;VintageStoryModManager.ModlistLoadMode;VintageStoryModManager.PresetLoadOptions;VintageStoryModManager.Services.CloudModlistCacheService;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.PresetFileLoader

### LoadModlistFromCloudMenuItem_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** (none)
- **Fields read:** _viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ApplyPresetAsync;CreateAutomaticBackupAsync;EnsureModlistBackupBeforeLoad;ExecuteCloudOperationAsync;GetModlistLoadOptions;PrepareForModlistLoad;PromptModlistLoadMode
- **Awaited calls:** ApplyPresetAsync;CloudModlistContentService.EnsureSlotContentAsync;CloudModlistSlotService.LoadSlotsAsync;CreateAutomaticBackupAsync("ModlistLoaded").ConfigureAwait;ExecuteCloudOperationAsync
- **External types used:** VintageStoryModManager.Models.CloudModlistSlot;VintageStoryModManager.Models.ModPreset;VintageStoryModManager.Models.ModPreset?;VintageStoryModManager.ModlistLoadMode;VintageStoryModManager.PresetLoadOptions;VintageStoryModManager.Services.CloudModlistContentService;VintageStoryModManager.Services.CloudModlistHelper;VintageStoryModManager.Services.CloudModlistSlotService;VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.PresetFileLoader;VintageStoryModManager.Views.Dialogs.CloudSlotSelectionDialog

### RefreshCloudModlistsAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _cloudModlistsLoaded;_isCloudModlistRefreshInProgress;_viewModel
- **Fields written:** _cloudModlistsLoaded;_isCloudModlistRefreshInProgress
- **Untracked MainWindow members used:** CloudModlistsDataGrid
- **MainWindow methods called:** ExecuteCloudOperationAsync;SetCloudModlistSelection;UpdateCloudModlistControlsEnabledState
- **Awaited calls:** Dispatcher.InvokeAsync;ExecuteCloudOperationAsync;store.GetRegistryEntriesAsync
- **External types used:** VintageStoryModManager.Services.CloudModlistHelper

### RefreshCloudModlistsButton_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** RefreshCloudModlistsButton.Click
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** RefreshCloudModlistsAsync
- **Awaited calls:** RefreshCloudModlistsAsync
- **External types used:** (none)

### SetCloudModlistSelection

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** _selectedCloudModlist
- **Untracked MainWindow members used:** SelectedModlistDescription;SelectedModlistTitle
- **MainWindow methods called:** UpdateCloudModlistControlsEnabledState
- **Awaited calls:** (none)
- **External types used:** (none)

### UpdateCloudModlistControlsEnabledState

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _isCloudModlistRefreshInProgress;_selectedCloudModlist
- **Fields written:** (none)
- **Untracked MainWindow members used:** InstallCloudModlistButton;ModifyCloudModlistsButton;RefreshCloudModlistsButton;SaveCloudModlistButton
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.InternetAccessManager

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.CloudManagement.cs | DeleteAllCloudModlistsAndAuthorizationAsync | SetCloudModlistSelection |
| MainWindow.CloudManagement.cs | UpdateCloudModlistsAfterChangeAsync | RefreshCloudModlistsAsync |
| MainWindow.CloudSave.cs | SaveCloudModlistButton_OnClick | RefreshCloudModlistsAsync |
| MainWindow.CloudSave.cs | SaveModlistToCloudMenuItem_OnClick | RefreshCloudModlistsAsync |
| MainWindow.InternetAccess.cs | InternetAccessManager_OnInternetAccessChanged | UpdateCloudModlistControlsEnabledState |
| MainWindow.Navigation.cs | HandleModlistsVisibilityChanged | RefreshCloudModlistsAsync |
| MainWindow.Navigation.cs | HandleModlistsVisibilityChanged | SetCloudModlistSelection |
| MainWindow.Navigation.cs | ModlistsTabControl_OnSelectionChanged | RefreshCloudModlistsAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| DeleteCloudModlistMenuItem_OnClick | MainWindow.CloudInfrastructure.cs | ExecuteCloudOperationAsync |
| EnsureCloudModlistContentAsync | MainWindow.CloudInfrastructure.cs | ExecuteCloudOperationAsync |
| InstallCloudModlistButton_OnClick | MainWindow.FeatureDirectories.cs | EnsureCloudModListCacheDirectory |
| InstallCloudModlistButton_OnClick | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| InstallCloudModlistButton_OnClick | MainWindow.ModlistLoad.cs | EnsureModlistBackupBeforeLoad |
| InstallCloudModlistButton_OnClick | MainWindow.ModlistLoad.cs | GetModlistLoadOptions |
| InstallCloudModlistButton_OnClick | MainWindow.ModlistLoad.cs | PromptModlistLoadMode |
| InstallCloudModlistButton_OnClick | MainWindow.ModlistLoading.cs | PrepareForModlistLoad |
| InstallCloudModlistButton_OnClick | MainWindow.PresetApplication.cs | ApplyPresetAsync |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.CloudInfrastructure.cs | ExecuteCloudOperationAsync |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.ModlistLoad.cs | CreateAutomaticBackupAsync |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.ModlistLoad.cs | EnsureModlistBackupBeforeLoad |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.ModlistLoad.cs | GetModlistLoadOptions |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.ModlistLoad.cs | PromptModlistLoadMode |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.ModlistLoading.cs | PrepareForModlistLoad |
| LoadModlistFromCloudMenuItem_OnClick | MainWindow.PresetApplication.cs | ApplyPresetAsync |
| RefreshCloudModlistsAsync | MainWindow.CloudInfrastructure.cs | ExecuteCloudOperationAsync |

## Field Ownership Notes

**Used only by this focused partial:**

- _isCloudModlistRefreshInProgress

**Used mostly by this focused partial:**

- _cloudModlistsLoaded
- _selectedCloudModlist

**Shared broadly across MainWindow:**

- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (5)

- **CloudModlistsDataGrid_OnSelectionChanged** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **DeleteCloudModlistMenuItem_OnClick** — fields: _cloudModlistsLoaded;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **InstallCloudModlistButton_OnClick** — fields: _selectedCloudModlist;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **LoadModlistFromCloudMenuItem_OnClick** — fields: _viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.
- **RefreshCloudModlistsButton_OnClick** — fields: (none) — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (3)

- **RefreshCloudModlistsAsync** — fields: _cloudModlistsLoaded;_isCloudModlistRefreshInProgress;_viewModel — references untracked MainWindow member(s) (CloudModlistsDataGrid) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **SetCloudModlistSelection** — fields: _selectedCloudModlist — references untracked MainWindow member(s) (SelectedModlistDescription;SelectedModlistTitle) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.
- **UpdateCloudModlistControlsEnabledState** — fields: _isCloudModlistRefreshInProgress;_selectedCloudModlist — references untracked MainWindow member(s) (InstallCloudModlistButton;ModifyCloudModlistsButton;RefreshCloudModlistsButton;SaveCloudModlistButton) — likely XAML-generated controls or bound UI properties; not safe to call "pure" or extract as a plain helper until those are abstracted behind an interface.

### high-risk due to shared state (1)

- **EnsureCloudModlistContentAsync** — fields: _viewModel — touches field(s) shared broadly across MainWindow (_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
