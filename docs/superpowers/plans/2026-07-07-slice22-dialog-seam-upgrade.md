# Slice 22: Dialog-Seam Upgrade (IConfirmationService over MessageDialogWindow) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-07-07-dialog-seam-design.md` (committed, approved 2026-07-07) — read it first.

**Goal:** Rebase `IConfirmationService` onto the app's standard themed dialog (`ModManagerMessageBox`/`MessageDialogWindow`), add severity + custom button text, fix slice 18's two dialog defects (Delete/Cancel mislabel on the auto-refresh confirm; lost Warning icons on notifies), and delete the now-redundant `ThemedConfirmationDialog`.

**Architecture:** Pure seam work — the interface gains optional parameters (existing call sites stay source-compatible), the service implementation swaps backends, three consumers get intent-restoring severity/text arguments, one dialog class dies. No new services, no VM extractions.

**Tech Stack:** .NET 8 WPF, xUnit.

## Global Constraints

- **Base commit: orchestrator-supplied tip** (anchors verified at `bb6cf25`; this slice's files are disjoint from slice 21's — parallel-safe).
- Release build zero warnings/errors; **solution-wide unscoped IDE0005** `--verify-no-changes` clean; full test suite passes (264 at plan time).
- **Move/behavior discipline:** the only intended behavior changes are the three dialog-appearance fixes named in Task 2/3 (correct buttons + icons). Everything else — messages, titles, decline semantics, call ordering — byte-identical. Enumerate every change under "Adaptations".
- No `Views/` XAML changes except deleting `ThemedConfirmationDialog.xaml` (+ its `.xaml.cs`).
- Do NOT push; never touch `CLAUDE.md` or stage `.claude/`.
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` in own worktree only; base-proof: `docs/superpowers/specs/2026-07-07-dialog-seam-design.md` exists. Stop-and-report on anomalies.

**Anchors (at `bb6cf25`):** `Services/IConfirmationService.cs` (2 members), `Services/ConfirmationService.cs` (ConfirmAsync → ThemedConfirmationDialog; NotifyAsync → ModManagerMessageBox OK/Information), `Views/Dialogs/ThemedConfirmationDialog.xaml[.cs]` (zero call sites outside ConfirmationService — re-verify), `Views/Dialogs/MessageDialogTypes.cs` (`MessageDialogButtonContentOverrides` with `Ok/Cancel/Yes/No` init-props), `Services/ModManagerMessageBox.cs` (`Show(text, caption, MessageBoxButton, MessageBoxImage, MessageDialogExtraButton? = null, MessageDialogButtonContentOverrides? = null)`), consumers: `ViewModels/SettingsMenuViewModel.cs` (ConfirmAsync ~line 239, NotifyAsync ~259/272), `ViewModels/SyncToServerDialogViewModel.cs` (ConfirmAsync ~380, server-deletion confirm), fake: `VintageStoryModManager.Tests/SettingsMenuViewModelTests.cs:81–100` (`FakeConfirmation`).

---

### Task 1: Interface + service reimplementation

**Files:**
- Modify: `VintageStoryModManager/Services/IConfirmationService.cs`
- Modify: `VintageStoryModManager/Services/ConfirmationService.cs`

**Interfaces — Produces (exact contents):**

`IConfirmationService.cs`:

```csharp
namespace VintageStoryModManager.Services
{
    /// <summary>Severity of a seam dialog; maps to the themed dialog's icon.</summary>
    public enum DialogSeverity
    {
        Information,
        Warning,
        Error,
        Question
    }

    /// <summary>
    ///     Service for user confirmation and notification dialogs, backed by the app's standard
    ///     themed message dialog. Lets ViewModels prompt without direct window dependencies.
    /// </summary>
    public interface IConfirmationService
    {
        /// <summary>Yes/No-style confirmation. Custom button text via confirmText/cancelText (null = Yes/No).</summary>
        Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null);

        /// <summary>OK-only notification.</summary>
        Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information);
    }
}
```

`ConfirmationService.cs`:

```csharp
using System.Windows;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Services
{
    /// <summary>
    ///     Default implementation of IConfirmationService over ModManagerMessageBox
    ///     (the app-standard themed MessageDialogWindow).
    /// </summary>
    public class ConfirmationService : IConfirmationService
    {
        public Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null)
        {
            MessageDialogButtonContentOverrides? overrides = null;
            if (confirmText is not null || cancelText is not null)
                overrides = new MessageDialogButtonContentOverrides { Yes = confirmText, No = cancelText };

            var result = ModManagerMessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MapSeverity(severity),
                buttonContentOverrides: overrides);

            return Task.FromResult(result == MessageBoxResult.Yes);
        }

        public Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information)
        {
            ModManagerMessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MapSeverity(severity));
            return Task.CompletedTask;
        }

        private static MessageBoxImage MapSeverity(DialogSeverity severity) => severity switch
        {
            DialogSeverity.Warning => MessageBoxImage.Warning,
            DialogSeverity.Error => MessageBoxImage.Error,
            DialogSeverity.Question => MessageBoxImage.Question,
            _ => MessageBoxImage.Information
        };
    }
}
```

(Check `ModManagerMessageBox.Show`'s parameter order before writing — the anchors say `(text, caption, button, image, extraButton?, buttonContentOverrides?)`; use the named argument `buttonContentOverrides:` as above. If the overrides parameter name differs, match the real one. Note the old ConfirmAsync's owner handling — `Owner = Application.Current?.MainWindow` — is now inside `ModManagerMessageBox.ShowInternal`'s owner resolution; no explicit owner needed. List as an Adaptation.)

- [ ] **Step 1:** Re-verify zero external `ThemedConfirmationDialog` usage: `grep -rn 'ThemedConfirmationDialog' --include='*.cs' --include='*.xaml' VintageStoryModManager/ | grep -v obj/` → only its own two files + `ConfirmationService.cs`.
- [ ] **Step 2:** Write both files as above. Build will FAIL at this point only if consumer signatures mismatch — the optional parameters keep `ConfirmAsync(msg, title)`/`NotifyAsync(msg, title)` call sites compiling; the test fake will fail to compile (fixed in Task 3). Expected: `dotnet build VintageStoryModManager/VintageStoryModManager.csproj --configuration Release` → 0/0 for the app project.
- [ ] **Step 3:** Commit:

```bash
git add VintageStoryModManager/Services/IConfirmationService.cs VintageStoryModManager/Services/ConfirmationService.cs
git commit -m "refactor: rebase IConfirmationService onto themed MessageDialogWindow with severity (slice 22, dialog seam)"
```

---

### Task 2: Consumer intent restoration + delete ThemedConfirmationDialog

**Files:**
- Modify: `VintageStoryModManager/ViewModels/SettingsMenuViewModel.cs`
- Modify: `VintageStoryModManager/ViewModels/SyncToServerDialogViewModel.cs`
- Delete: `VintageStoryModManager/Views/Dialogs/ThemedConfirmationDialog.xaml`, `VintageStoryModManager/Views/Dialogs/ThemedConfirmationDialog.xaml.cs`

**Changes (each restores pre-slice-18 or intended semantics — these ARE the slice's behavior changes, list all three as Adaptations):**

1. `SettingsMenuViewModel.ToggleDisableAutoRefreshAsync` (~239): `await _confirmation.ConfirmAsync(message, "Simple VS Manager")` → `await _confirmation.ConfirmAsync(message, "Simple VS Manager", DialogSeverity.Warning)` — restores the original pre-slice-18 `MessageBoxButton.YesNo, MessageBoxImage.Warning` exactly.
2. `SettingsMenuViewModel.ToggleAutomaticDataBackupsAsync` invalid-data-dir notify (~259): add `DialogSeverity.Warning` — restores the original Warning icon. The first-enable info notify (~272) stays default (Information — matches original).
3. `SyncToServerDialogViewModel` (~380): `ConfirmAsync(message, "Confirm Deletions")` → `ConfirmAsync(message, "Confirm Deletions", DialogSeverity.Warning, confirmText: "Delete")` — preserves the Delete-labeled destructive confirm this call visually had under the retired dialog.
4. Delete both `ThemedConfirmationDialog` files (`git rm`).

- [ ] **Step 1:** Apply the three call-site changes; `git rm` the two dialog files.
- [ ] **Step 2:** Stale grep: `grep -rn 'ThemedConfirmationDialog' --include='*.cs' --include='*.xaml' VintageStoryModManager/ | grep -v obj/` → **zero hits**.
- [ ] **Step 3:** `dotnet build VintageStoryModManager/VintageStoryModManager.csproj --configuration Release` → 0/0.
- [ ] **Step 4:** Commit:

```bash
git add -u VintageStoryModManager/
git commit -m "refactor: restore dialog severities and retire ThemedConfirmationDialog (slice 22, dialog seam)"
```

---

### Task 3: Test updates

**Files:**
- Modify: `VintageStoryModManager.Tests/SettingsMenuViewModelTests.cs` (the `FakeConfirmation` at lines 81–100 + assertions)

**`FakeConfirmation` new shape (records the new parameters):**

```csharp
private sealed class FakeConfirmation : IConfirmationService
{
    public bool ConfirmAnswer { get; set; }
    public int ConfirmCalls { get; private set; }
    public int NotifyCalls { get; private set; }
    public List<(string Message, string Title, DialogSeverity Severity)> NotifyMessages { get; } = new();
    public List<(string Message, string Title, DialogSeverity Severity, string? ConfirmText, string? CancelText)> ConfirmRequests { get; } = new();

    public Task<bool> ConfirmAsync(string message, string title,
        DialogSeverity severity = DialogSeverity.Question,
        string? confirmText = null, string? cancelText = null)
    {
        ConfirmCalls++;
        ConfirmRequests.Add((message, title, severity, confirmText, cancelText));
        return Task.FromResult(ConfirmAnswer);
    }

    public Task NotifyAsync(string message, string title,
        DialogSeverity severity = DialogSeverity.Information)
    {
        NotifyCalls++;
        NotifyMessages.Add((message, title, severity));
        return Task.CompletedTask;
    }
}
```

Existing assertions on `NotifyMessages` tuples need the extra tuple element (compiler will point at each). Add three assertions to existing tests (no new test files):
1. In the accepted auto-refresh test: `Assert.Equal(DialogSeverity.Warning, fixture.Confirmation.ConfirmRequests.Single().Severity);`
2. In the invalid-data-dir backups test: the recorded notify's `Severity == DialogSeverity.Warning`.
3. In the first-enable backups test: the recorded notify's `Severity == DialogSeverity.Information`.

If `SyncToServerDialogViewModel` has tests with their own fake (grep `IConfirmationService` in `VintageStoryModManager.Tests/`), update that fake identically; if it has none, note it in the report — do not add new test files.

- [ ] **Step 1:** Update fake + assertions; `dotnet test VintageStoryModManager.Tests --filter SettingsMenuViewModelTests` → all pass. (Known `*_wpftmp` flake: re-run once, then `dotnet clean ./ImprovedModMenu.sln --configuration Debug`.)
- [ ] **Step 2:** Full suite Release → all pass.
- [ ] **Step 3:** Commit:

```bash
git add VintageStoryModManager.Tests/
git commit -m "test: cover dialog-seam severities in SettingsMenuViewModel tests (slice 22, dialog seam)"
```

---

### Task 4: Final gates + report

- [ ] `dotnet build ./ImprovedModMenu.sln --configuration Release` → 0/0.
- [ ] Solution-wide unscoped IDE0005 `--verify-no-changes` → clean.
- [ ] Full suite Release → all pass.
- [ ] `git diff --name-only` off base: exactly `IConfirmationService.cs`, `ConfirmationService.cs`, `SettingsMenuViewModel.cs`, `SyncToServerDialogViewModel.cs`, the two deleted `ThemedConfirmationDialog` files, and the test file(s) — nothing else.
- [ ] Report: worktree path + branch, commit hashes, Adaptations (including the three intended behavior changes and the owner-resolution note), the SyncToServerDialogViewModel-tests finding, test results. No push; no `CLAUDE.md`/`.claude/`.

---

## Smoke test (orchestrator/user, post-merge)

Settings → Disable Auto Refresh with acknowledgment cleared → the standard themed message dialog appears with **Yes/No buttons and the Warning icon** (not Delete/Cancel); decline → checkmark stays off; accept → sticks. Automatic Data Backups with no valid data dir → Warning-icon notify; first valid enable → Information notify shown once. Server sync with pending deletions → confirm shows **Delete/Cancel** (labeled) with Warning icon; cancel aborts the sync. Spot-check two unrelated dialogs (e.g. a mod-deletion confirm, a Help dialog) → unchanged.
