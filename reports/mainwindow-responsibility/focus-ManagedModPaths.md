# Focused Report: MainWindow.ManagedModPaths.cs

## Header

- **Focused partial file:** MainWindow.ManagedModPaths.cs
- **Line count:** 53
- **Method count:** 1
- **Event handler count:** 0

## Method Inventory

### TryGetManagedModPath

- **Async:** no
- **Event-handler-like:** no
- **XAML events:** (none)
- **Fields read:** _dataDirectory
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** (none)
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ManagedModPathHelper

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

| Caller file | Caller method | Called method (this partial) |
|---|---|---|
| MainWindow.ExclusivePresetApplication.cs | ApplyExclusivePresetAsync | TryGetManagedModPath |
| MainWindow.ModDeletion.cs | DeleteMultipleModsAsync | TryGetManagedModPath |
| MainWindow.ModDeletion.cs | DeleteSingleModAsync | TryGetManagedModPath |
| MainWindow.ModDependencyRepair.cs | InstallOrUpdateDependencyAsync | TryGetManagedModPath |
| MainWindow.ModUpdateCommands.cs | UpdateModsAsync | TryGetManagedModPath |

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

(none)

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _dataDirectory

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

- **TryGetManagedModPath** — fields: _dataDirectory — touches field(s) shared broadly across MainWindow (_dataDirectory); moving it risks splitting shared state across files.

## Risk Summary

- **Risk level:** high
- **Reasons:** 1 method(s) touch fields shared broadly across MainWindow; extracting this partial requires resolving that shared state first.
