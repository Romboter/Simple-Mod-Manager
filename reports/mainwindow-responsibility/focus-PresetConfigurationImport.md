# Focused Report: MainWindow.PresetConfigurationImport.cs

## Header

- **Focused partial file:** MainWindow.PresetConfigurationImport.cs
- **Line count:** 136
- **Method count:** 1
- **Event handler count:** 0

## Method Inventory

### ImportPresetConfigsAsync

- **Async:** yes
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory;_userConfiguration;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** UpdateSelectedModButtons
- **Awaited calls:** PresetConfigurationImportService.ImportAsync(
                        configurations,
                        _dataDirectory,
                        _userConfiguration,
                        modDisplayNames)
                    .ConfigureAwait
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.Services.PresetConfigurationImportEntry;VintageStoryModManager.Services.PresetConfigurationImportResult;VintageStoryModManager.Services.PresetConfigurationImportService

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.PresetApplication.cs | ApplyPresetAsync | ImportPresetConfigsAsync |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| ImportPresetConfigsAsync | MainWindow.ModGridSelection.cs | UpdateSelectedModButtons |

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

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (0)

(none)

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (1)

- **ImportPresetConfigsAsync** — fields: _dataDirectory;_userConfiguration;_viewModel — touches field(s) shared broadly across MainWindow (_dataDirectory;_userConfiguration;_viewModel); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
