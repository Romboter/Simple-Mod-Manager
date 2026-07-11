# MVVM Command Pattern — Design (Sub-project 1 of the full MVVM phase)

**Status:** Approved 2026-07-11
**Scope:** Establish the handler→command conversion pattern and prove it on one pilot area (H, Server sync). Sub-project 2 (rolling the pattern out to areas A–M) is a separate, later effort — probably its own roadmap doc, not a design doc, once this pilot lands.

## Background

CLAUDE.md's move-don't-rewrite refactor phase is complete (2026-07-11): `MainWindow.xaml.cs` is decomposed into 74 responsibility-based partials, logic is extracted into services/ViewModels, and a themed dialog seam (`IConfirmationService`) replaced almost all raw `MessageBox` calls. What's left is real MVVM work — converting XAML `Click=`/event-handler wiring to `Command` bindings against ViewModel-exposed commands — which was deliberately deferred out of that phase (see `docs/superpowers/specs/2026-07-11-area-b-design-lite.md` and `docs/superpowers/specs/2026-07-11-init-time-dialogs-notes.md`).

"Full MVVM" touches all of areas A–M and is too large for one spec. This document scopes only the first sub-project: design the command pattern and prove it once, on a small real area, before a rollout pipeline is planned.

### Existing groundwork found during this design pass

- `CommunityToolkit.Mvvm` 8.4.0 is already a project dependency. `ObservableObject` (the base class) is already used throughout `ViewModels/`. Its command half — `[RelayCommand]`/`[ObservableProperty]` source generators — is effectively unused.
- One hand-rolled precedent already exists: `MainViewModel._clearSearchCommand` (a manually-constructed `RelayCommand`), bound in `App.xaml` via `Command="{Binding DataContext.ClearSearchCommand, RelativeSource={RelativeSource AncestorType=TextBox}}"`. Proves XAML command binding against this ViewModel family already works in this codebase.
- `docs/BEST_PRACTICES_REFACTORING.md` (pre-existing, aspirational) already recommends `[RelayCommand]`/`[ObservableProperty]` over manual wiring — this design formalizes and scopes that recommendation rather than inventing a new direction.
- `IConfirmationService` (the message-dialog seam) is owned and instantiated by `MainWindow`, and is already injected into a couple of extracted sub-ViewModels (`SettingsMenuViewModel`, `SyncToServerDialogViewModel`) — but not into `MainViewModel` itself. This design follows that same injection precedent for new sub-ViewModels.
- `IConfirmationService` only covers simple message/confirmation dialogs. It does **not** cover launching a real dialog `Window` (e.g. `ManageServerTargetsDialog`, `SyncToServerDialog`) that needs an `Owner`. The pilot area needs this, so this design adds a second, parallel seam for it (Section C below).

## A. Command mechanism

Use `CommunityToolkit.Mvvm`'s `[RelayCommand]` and `[ObservableProperty]` source generators. No new dependency — the package is already referenced. Sub-ViewModels become `partial class`; a method tagged `[RelayCommand]` generates an `IRelayCommand`-typed property that XAML binds to directly (`Command="{Binding SyncToServerCommand}"`). `[RelayCommand(CanExecute = nameof(CanSync))]` replaces today's imperative `control.IsEnabled = ...` pushes; calling `SyncToServerCommand.NotifyCanExecuteChanged()` (or letting the generator wire it via `[NotifyCanExecuteChangedFor]`) replaces manual `UpdateXState()` methods.

## B. Command home: per-area sub-ViewModels

Commands and their state live on small, per-area ViewModels — continuing the pattern Area M's decomposition already established (`TabNavigationViewModel`, `SettingsMenuViewModel`, `ModlistCollectionsViewModel`, etc.) — rather than centralizing everything on `MainViewModel`. `MainViewModel` stays a coordinator; it does not regrow past its post-decomposition size (4,749 → 2,353 lines) as more areas convert.

## C. Dialog-launching seam (new)

`IConfirmationService` covers message/confirmation dialogs only. A parallel, minimal seam covers launching a real dialog `Window`:

```csharp
public interface IDialogLauncher
{
    bool? ShowDialog(Window dialog);
}
```

Implemented by `MainWindow`:

```csharp
public bool? ShowDialog(Window dialog)
{
    dialog.Owner = this;
    return dialog.ShowDialog();
}
```

Injected into sub-ViewModels alongside `IConfirmationService`. A command that needs to open a dialog window constructs the dialog + its own ViewModel (as today) and calls `_dialogLauncher.ShowDialog(dialog)` instead of setting `Owner = this` inline. Deliberately minimal: no generic `ShowDialog<TResult>`, no dialog-result abstraction beyond the existing `bool?` — nothing in the pilot needs more than that, and speculative generality isn't warranted yet.

## D. Wiring / composition

No DI container is introduced (`docs/BEST_PRACTICES_REFACTORING.md`'s DI-container suggestion is explicitly out of scope for this pass). `MainWindow` stays the manual composition root, matching every other extraction in this codebase's history: it constructs each area's ViewModel with its required services plus `_confirmationService` and `this` (as `IDialogLauncher`), exposes it as a property, and XAML binds against it — either via a `DataContext` split for that section of the tree, or a `{Binding ServerSync.SyncToServerCommand}` path from the existing root DataContext, whichever keeps the XAML diff smallest per control.

## E. Pilot conversion: Area H (Server sync)

Smallest real area (4 XAML handlers, services already extracted — `ServerSyncPreflight`, `ServerConnectionHelper`, `ServerCommandBuilder`, `SyncEngine`). Converts `MainWindow.ServerSync.cs` + `MainWindow.ServerModCopy.cs` into a new `ViewModels/ServerSyncViewModel.cs`:

| Today (code-behind handler) | Becomes |
|---|---|
| `EnableServerOptionsMenuItem_OnClick` (reads `MenuItem.IsChecked`, persists via `UserConfigurationService`, imperatively pushes `Visibility`/`IsChecked` to 4 other controls) | `[ObservableProperty] bool IsServerOptionsEnabled` two-way bound to the menu item's `IsChecked`; a `partial void OnIsServerOptionsEnabledChanged(bool value)` persists the setting; dependent controls' `Visibility`/`IsEnabled` bind to `IsServerOptionsEnabled` (via a `BooleanToVisibilityConverter` for the Visibility cases) instead of being pushed to imperatively |
| `ManageServerTargetsMenuItem_OnClick` (builds `ManageServerTargetsDialog`, sets `Owner = this`, `ShowDialog()`, then calls `UpdateSyncToServerMenuState()`) | `[RelayCommand] void ManageServerTargets()` — builds the dialog, calls `_dialogLauncher.ShowDialog(dialog)`, then re-evaluates `CanSyncToServer` |
| `SyncToServerMenuItem_OnClick` (preflight via `ServerSyncPreflight.Evaluate`, notify-on-failure via `_confirmationService`, else builds `SyncToServerDialogViewModel` + `SyncToServerDialog`, `Owner = this`, `ShowDialog()`) | `[RelayCommand(CanExecute = nameof(CanSyncToServer))] async Task SyncToServerAsync()` — same preflight/notify logic, dialog launch via `_dialogLauncher` |
| `SelectedModCopyForServerButton_OnClick` (reads `Button.DataContext` for the mod, builds clipboard command, reports status, notifies on `ExternalException`) | `[RelayCommand] void CopyServerInstallCommand(ModListItemViewModel mod)` bound via `CommandParameter="{Binding}"` on the DataGrid row button — this is also the pattern later areas reuse for other per-row buttons (Fix, Delete, Install) |

Characterization tests for `ServerSyncViewModel` follow the existing sub-ViewModel test style (e.g. `SettingsMenuViewModelTests`).

Verification: `dotnet build` Release zero-warnings, full test suite green, manual smoke test of all 4 converted controls (toggle server options, manage targets, sync to server success + preflight-failure paths, copy install command success + clipboard-failure paths) — added as a checklist doc per CLAUDE.md's smoke-test rule.

## F. Explicitly out of scope for this sub-project

- **Area B's view-state coupling** (`UpdateModsAsync`'s busy flag/overlay/selection-button refresh) and **the init-time-dialogs constructor boundary** (`MainWindow.PathInitialization.cs`, `HandleViewModelInitializationFailure`) are not touched here. Both are real candidates for the pattern this design establishes, but get their own later pass once the pattern is proven — not folded into the pilot.
- **Areas A, C, D, E, G, I, J, K** (the rest of the handler→command rollout) — sub-project 2, planned only after this pilot lands and its smoke test passes.
- **DI container adoption** — out of scope; manual composition stays the pattern.
- **Generic dialog-result abstraction** beyond `bool?` — not needed yet; add when a concrete case needs it.
