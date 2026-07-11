# Dialog-Seam Design: IConfirmationService over MessageDialogWindow

**Created:** 2026-07-07 (brainstormed and approved in-session)
**Status:** Approved by user 2026-07-07
**Purpose:** Make the deferred `IConfirmationService` "product decision" deliberate, fix two slice-18 defects, and unblock the coordinator/MVVM work deferred in areas D (Backups), C (Modlists), G (Cloud), and B (Mod operations).

## Survey ground truth (2026-07-07, 233 call sites in 56 files)

- `ModManagerMessageBox` is NOT an OS message box: it's a static facade over the custom themed `MessageDialogWindow` (`Views/Dialogs/`), with icon rendering, copy-to-clipboard, `MessageDialogButtonContentOverrides` (per-button text), and an optional `MessageDialogExtraButton`. Zero raw `System.Windows.MessageBox` calls exist anywhere.
- Usage: ~87% notify-only (`MessageBoxButton.OK`, 208 of 250 enum occurrences); confirms are YesNo 29, OKCancel 8, YesNoCancel 5.
- `ThemedConfirmationDialog` (current `ConfirmAsync` backend) is the cruder dialog: hard-coded **"Delete"/"Cancel"** buttons + ⚠️ glyph, delete-flavored, zero call sites outside `ConfirmationService`.
- Known defects this design fixes: slice 18's auto-refresh confirmation shows "Delete"/"Cancel" for a non-delete question, and its backup notifies lost the Warning icon (`NotifyAsync` hard-codes Information).

## Decisions

1. **Backend: `MessageDialogWindow` (via `ModManagerMessageBox`) is the app's one dialog look.** `ThemedConfirmationDialog` retires (deleted; its delete-flavored UX is reproduced by `ConfirmAsync(..., DialogSeverity.Warning, confirmText: "Delete")`).
2. **Scope: migrate-as-areas-extract.** No app-wide sweep of the 233 sites. Dialog calls convert to the seam only when their surrounding logic moves into a coordinator/VM in an area slice. Code-behind that stays code-behind keeps calling `ModManagerMessageBox` directly.

## Seam API (evolves the existing interface; optional params keep current call sites source-compatible)

```csharp
public enum DialogSeverity { Information, Warning, Error, Question }

public interface IConfirmationService
{
    Task<bool> ConfirmAsync(string message, string title,
        DialogSeverity severity = DialogSeverity.Question,
        string? confirmText = null, string? cancelText = null);   // null → Yes/No defaults
    Task NotifyAsync(string message, string title,
        DialogSeverity severity = DialogSeverity.Information);
}
```

`ConfirmationService` implements both over `ModManagerMessageBox.Show`:
- severity → `MessageBoxImage` map (Information/Warning/Error/Question).
- `ConfirmAsync` → `MessageBoxButton.YesNo`, with `MessageDialogButtonContentOverrides { Yes = confirmText, No = cancelText }` when custom text is supplied; returns `result == MessageBoxResult.Yes`.
- `NotifyAsync` → `MessageBoxButton.OK` with the mapped icon.

## Out of seam scope (permanent non-goals unless a need arises)

- The ~18 input-collecting dialogs (text entry, list/slot selection) — a different pattern; VM-owned dialog services later, if ever.
- YesNoCancel flows (`MainWindow.ModlistLoad.cs` ×2) and extra-button/suppress flows — stay direct `ModManagerMessageBox` calls even inside extracted coordinators (passed through as delegates if a coordinator needs one).

## Consequences

- **Slice 22** (small, file-disjoint from slice 21, parallel-safe): implement the API above, update the 3 existing consumers (`SettingsMenuViewModel` — restore Warning severities; `SyncToServerDialogViewModel` — its server-deletion confirm becomes `DialogSeverity.Warning, confirmText: "Delete"`; test fakes), delete `ThemedConfirmationDialog`.
- **Unblocked slice queue after 21/22:** area D backups coordinator, area C modlist workflow, area G cloud flows, area B `UpdateModsAsync` view-state move — each gets its own design-lite + plan when reached; the seam decision is no longer a blocker for any of them.
