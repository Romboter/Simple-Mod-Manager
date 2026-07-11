# Focused Report: MainWindow.ModConfigScanning.cs

## Header

- **Focused partial file:** MainWindow.ModConfigScanning.cs
- **Line count:** 236
- **Method count:** 4
- **Event handler count:** 1

## Method Inventory

### ScanForModConfigFilesAsync

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ScanForModConfigFilesAsync
- **Awaited calls:** (none)
- **External types used:** (none)

### ScanForModConfigFilesAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** (none)
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ScanForModConfigFilesAsync
- **Awaited calls:** ScanForModConfigFilesAsync(viewModel, candidateMods).ConfigureAwait
- **External types used:** VintageStoryModManager.ViewModels.ModListItemViewModel

### ScanForModConfigFilesAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_userConfiguration
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** UpdateSelectedModEditConfigButton
- **Awaited calls:** Task.Run(() => ModConfigurationMatcher.FindConfigMatches(missingMods, configFiles)).ConfigureAwait
- **External types used:** VintageStoryModManager.Helpers.ModConfigPathHelper;VintageStoryModManager.Services.ModConfigurationMatcher;VintageStoryModManager.Services.StatusLogService;VintageStoryModManager.ViewModels.ModListItemViewModel

### ScanForModConfigsMenuItem_OnClick

- **Async:** yes
- **Event-handler-like:** yes
- **XAML events:** MenuItem.Click
- **Fields read:** _dataDirectory;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** ScanForModConfigFilesAsync
- **Awaited calls:** ScanForModConfigFilesAsync(_viewModel).ConfigureAwait
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

(none)

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| ScanForModConfigFilesAsync | MainWindow.ModGridSelection.cs | UpdateSelectedModEditConfigButton |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory
- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (2)

- **ScanForModConfigFilesAsync** — fields: (none) — touches field(s) owned mostly by this partial ((none)); candidate for a scoped service once similar methods are grouped.
- **ScanForModConfigFilesAsync** — fields: (none) — touches field(s) owned mostly by this partial ((none)); candidate for a scoped service once similar methods are grouped.

### keep in MainWindow because UI/XAML-bound (1)

- **ScanForModConfigsMenuItem_OnClick** — fields: _dataDirectory;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (1)

- **ScanForModConfigFilesAsync** — fields: _dataDirectory;_userConfiguration — touches field(s) shared broadly across MainWindow (_dataDirectory;_userConfiguration); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
