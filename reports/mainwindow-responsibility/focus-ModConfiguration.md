# Focused Report: MainWindow.ModConfiguration.cs

## Header

- **Focused partial file:** MainWindow.ModConfiguration.cs
- **Line count:** 104
- **Method count:** 1
- **Event handler count:** 1

## Method Inventory

### EditConfigButton_OnClick

- **Async:** no
- **Event-handler-like:** yes
- **XAML events:** SelectedModEditConfigButton.Click
- **Fields read:** _userConfiguration;_viewModel
- **Fields written:** (none)
- **Untracked MainWindow members used:** (none)
- **MainWindow methods called:** PromptForConfigFile;UpdateSelectedModEditConfigButton
- **Awaited calls:** (none)
- **External types used:** VintageStoryModManager.Services.ModManagerMessageBox;VintageStoryModManager.ViewModels.ModConfigEditorViewModel;VintageStoryModManager.ViewModels.ModListItemViewModel;VintageStoryModManager.Views.ModConfigEditorWindow;YamlDotNet.Core.YamlException

## Incoming Dependencies

Methods in other MainWindow partials that call methods in this focused partial.

(none)

## Outgoing Dependencies

Methods in this focused partial that call methods in other MainWindow partials.

| Method (this partial) | Called file | Called method |
|---|---|---|
| EditConfigButton_OnClick | MainWindow.ModGridSelection.cs | UpdateSelectedModEditConfigButton |
| EditConfigButton_OnClick | MainWindow.PathSelection.cs | PromptForConfigFile |

## Field Ownership Notes

**Used only by this focused partial:**

(none)

**Used mostly by this focused partial:**

(none)

**Shared broadly across MainWindow:**

- _userConfiguration
- _viewModel

## Extraction Seam Suggestions

### safe helper extraction (0)

(none)

### possible service extraction (0)

(none)

### keep in MainWindow because UI/XAML-bound (1)

- **EditConfigButton_OnClick** — fields: _userConfiguration;_viewModel — wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.

### keep in MainWindow because it touches UI/generated MainWindow members (0)

(none)

### high-risk due to shared state (0)

(none)

## Risk Summary

- **Risk level:** low
- **Reasons:** no methods touch broadly-shared fields or UI/generated members, and none are plausible service candidates beyond safe helpers; this partial looks low-risk to extract from.
