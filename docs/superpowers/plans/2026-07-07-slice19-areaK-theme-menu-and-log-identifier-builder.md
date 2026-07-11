# Slice 19 (Area K bite 2): ThemeMenuViewModel + Log-Identifier Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Second area-K MVVM bite: (A) replace the theme-menu checkmark-sync and selection logic in `MainWindow.Appearance.cs` (143 lines) with a bound `ThemeMenuViewModel`, following slice 18's window-owned-VM pattern; (B) extract the pure installed-mod log-identifier list building from `MainWindow.Debugging.cs` into a testable static builder (twin of slice 8's `InstalledModIdListBuilder`).

**Architecture:** Part A mirrors slice 18: the VM is view-owned (`MainWindow` constructs it and exposes it as a `ThemeMenu` property, bound via `RelativeSource AncestorType=Window`); the three built-in theme menu items become `IsChecked` OneWay bindings + a command; the **dynamic custom-theme submenu items stay code-behind-constructed** (genuine dynamic-menu view code — WPF can't mix static `Items` and `ItemsSource` on one `MenuItem` without CompositeCollection pain) but become thin: each created item binds `IsChecked` to a per-item VM and routes clicks through the VM's command. All selection state and theme-switching logic moves to the VM; `App.ApplyTheme` and `ClearScrollViewerCache` (view concerns) are injected as delegates. The `ThemePaletteEditorDialog` launch stays code-behind per the dialog-pass rule. Part B is a pure verbatim-move extraction with no XAML.

**Tech Stack:** .NET 8 WPF, CommunityToolkit `ObservableObject`/`RelayCommand`, xUnit.

## Global Constraints

- **Base commit: `3cc0a3f`** (post-slice-15/18 tip). Verified there on 2026-07-07 — current anchors: `MainWindow.Appearance.cs` is 143 lines (`ThemeMenuItem_OnClick` 12–44, `EditThemeMenuItem_OnClick` 46–49, `UpdateThemeMenuSelection` 51–68, `RefreshCustomThemeMenuItems` 70–118, `ShowCustomThemeEditor` 120–142); `MainWindow.Core.cs` has `_customThemeMenuItems` field at 304 and the ctor calls `RefreshCustomThemeMenuItems(); UpdateThemeMenuSelection(...)` at 352–353; XAML theme menu block is `MainWindow.xaml:330–361`; `MainWindow.Debugging.cs` identifier-building loop is 96–118 inside `ExperimentalAllModsDebuggingMenuItem_OnClick`; `MainViewModel.GetInstalledModsSnapshot()` (returns `IReadOnlyList<ModListItemViewModel>`) is `MainViewModel.cs:648`; `InstalledModLogIdentifier` is `internal readonly record struct InstalledModLogIdentifier(string SearchValue, string DisplayLabel)` in `VintageStoryModManager/Models/Debugging/InstalledModLogIdentifier.cs` (namespace `VintageStoryModManager`); `SurprisePaletteGenerator` is `internal static` in `Services/`. Re-run any grep you depend on; greps beat quoted numbers; stop-and-report on contradictions.
- **File-disjointness:** this slice touches `IUserConfigurationService.cs`, `SettingsMenuViewModelTests.cs`, new `ThemeMenuViewModel.cs`, `MainWindow.xaml`, `MainWindow.Appearance.cs`, `MainWindow.Core.cs`, `MainWindow.Debugging.cs`, new `Services/InstalledModLogIdentifierListBuilder.cs`, and new test files. **Disjoint from slices 16/17** (both confined to `MainViewModel.cs` + their own new `Services/` files) — can run in parallel with them.
- **`MainViewModel.cs` must NOT be touched** (area-M continuous rule: nothing may grow it; this slice has no reason to open it).
- **Behavior parity:** every theme action behaves exactly as before — same config writes, same `App.ApplyTheme` calls with the same palettes, same `ClearScrollViewerCache` call placement (note: the *named-theme* branch never called it; preserve that), same checkmark semantics including "clicking the already-active theme leaves it checked".
- **Move, don't rewrite** the selection/dispatch logic — conditions verbatim into the VM; enumerate every non-verbatim change under "Adaptations".
- Release build zero warnings/errors; scoped IDE0005 gate on changed files (solution-wide has known pre-existing drift — not yours to fix).
- `./verify-methods.sh` is untracked (won't exist in your worktree) and only scans `MainWindow.*.cs` — use `grep -rn 'Name(' --include='*.cs' VintageStoryModManager/ | grep -v obj/ | grep -v bin/` for method-count checks.
- **Worktree environment check (first action):** `git rev-parse --show-toplevel` under `.claude/worktrees/`; `git reset --hard <TIP-COMMIT>` (orchestrator-supplied) in your own worktree only; verify `VintageStoryModManager/ViewModels/SettingsMenuViewModel.cs` exists (proves the slice-18 base). Stop-and-report on anomalies. Standing permission to abandon a part (A and B are independent) rather than force it.

---

### Task 1: Seam — theme members on `IUserConfigurationService`

**Files:**
- Modify: `VintageStoryModManager/Services/IUserConfigurationService.cs`
- Modify: `VintageStoryModManager.Tests/SettingsMenuViewModelTests.cs` (its `FakeUserConfiguration` implements the interface and **will break** when members are added — extend it in the same commit)

**Interfaces — Produces** (signatures copied from `UserConfigurationService.cs` — property 202, methods 712/881/906/913/1255; verify each with grep before writing):

```csharp
// appended inside IUserConfigurationService
ColorTheme ColorTheme { get; }
bool TryActivateTheme(string? name);
IReadOnlyDictionary<string, string> GetThemePaletteColors();
string GetCurrentThemeName();
IReadOnlyList<string> GetCustomThemeNames();
void SetColorTheme(ColorTheme theme, IReadOnlyDictionary<string, string>? paletteOverride = null);
```

`UserConfigurationService` already has all six with exactly these shapes (it already declares `: IUserConfigurationService` since slice 18) — no change to it. The static `GetThemeDisplayName` stays static on the concrete class (not on the interface; the VM doesn't need it).

**`FakeUserConfiguration` additions** (in `SettingsMenuViewModelTests.cs` — keep its existing auto-property-bag style; this fake only needs to satisfy the compiler for the settings tests, so minimal faithful-enough behavior):

```csharp
public ColorTheme ColorTheme { get; set; } = ColorTheme.VintageStory;
public string CurrentThemeName { get; set; } = "Vintage Story";
public List<string> CustomThemeNames { get; } = new();
public Dictionary<string, string> ThemePaletteColors { get; } = new();
public List<(ColorTheme Theme, IReadOnlyDictionary<string, string>? Palette)> SetColorThemeCalls { get; } = new();
public string? LastActivatedThemeName { get; private set; }
public bool TryActivateThemeResult { get; set; } = true;

public bool TryActivateTheme(string? name)
{
    LastActivatedThemeName = name;
    if (!TryActivateThemeResult) return false;
    ColorTheme = ColorTheme.Custom;
    if (name is not null) CurrentThemeName = name;
    return true;
}

public IReadOnlyDictionary<string, string> GetThemePaletteColors() => ThemePaletteColors;
public string GetCurrentThemeName() => CurrentThemeName;
public IReadOnlyList<string> GetCustomThemeNames() => CustomThemeNames;

public void SetColorTheme(ColorTheme theme, IReadOnlyDictionary<string, string>? paletteOverride = null)
{
    SetColorThemeCalls.Add((theme, paletteOverride));
    ColorTheme = theme;
    if (theme != ColorTheme.Custom) CurrentThemeName = theme.ToString();
}
```

(If the fake's file already nests helper state differently, match its local style — the recorded-calls lists are what Task 3's tests consume.)

- [ ] **Step 1:** Grep-verify the six signatures on `UserConfigurationService`: `grep -n 'public ColorTheme ColorTheme\|public bool TryActivateTheme\|public IReadOnlyDictionary<string, string> GetThemePaletteColors\|public string GetCurrentThemeName\|public IReadOnlyList<string> GetCustomThemeNames\|public void SetColorTheme' VintageStoryModManager/Services/UserConfigurationService.cs` → all six present.
- [ ] **Step 2:** Add the interface members; extend the fake; `dotnet build ./ImprovedModMenu.sln --configuration Release` → 0/0; `dotnet test VintageStoryModManager.Tests --filter SettingsMenuViewModelTests` → all still pass.
- [ ] **Step 3:** Commit:

```bash
git add VintageStoryModManager/Services/IUserConfigurationService.cs VintageStoryModManager.Tests/SettingsMenuViewModelTests.cs
git commit -m "refactor: add theme members to IUserConfigurationService (slice 19 prep, area K)"
```

---

### Task 2: `ThemeMenuViewModel` + XAML bindings + rewire Appearance/Core

**Files:**
- Create: `VintageStoryModManager/ViewModels/ThemeMenuViewModel.cs`
- Modify: `VintageStoryModManager/Views/MainWindow.xaml` (lines ~330–361)
- Modify: `VintageStoryModManager/Views/MainWindow/MainWindow.Appearance.cs`
- Modify: `VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs` (property + ctor init + delete two sync lines)

**Interfaces — Produces (full class):**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Bound view-model for the Themes menu. Owns theme selection state and the switch/activate
///     logic; applying the theme to the running app and clearing view caches are window concerns
///     injected as callbacks. The window still constructs the custom-theme MenuItems (dynamic menu
///     population is view code) but binds each one to an entry in <see cref="CustomThemes" />.
/// </summary>
public sealed class ThemeMenuViewModel : ObservableObject
{
    private readonly IUserConfigurationService _configuration;
    private readonly Action<ColorTheme, IReadOnlyDictionary<string, string>?> _applyTheme;   // (t, p) => App.ApplyTheme(t, p)
    private readonly Action _clearScrollViewerCache;                                          // () => ClearScrollViewerCache()

    private bool _isVintageStorySelected;
    private bool _isDarkSelected;
    private bool _isLightSelected;

    public ThemeMenuViewModel(
        IUserConfigurationService configuration,
        Action<ColorTheme, IReadOnlyDictionary<string, string>?> applyTheme,
        Action clearScrollViewerCache)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _applyTheme = applyTheme ?? throw new ArgumentNullException(nameof(applyTheme));
        _clearScrollViewerCache = clearScrollViewerCache ?? throw new ArgumentNullException(nameof(clearScrollViewerCache));

        SelectBuiltInThemeCommand = new RelayCommand<ColorTheme>(SelectBuiltInTheme);
        SelectCustomThemeCommand = new RelayCommand<string>(SelectCustomTheme);

        RefreshFromConfiguration();
    }

    public bool IsVintageStorySelected => _isVintageStorySelected;
    public bool IsDarkSelected => _isDarkSelected;
    public bool IsLightSelected => _isLightSelected;

    public ObservableCollection<CustomThemeMenuItemViewModel> CustomThemes { get; } = new();

    public IRelayCommand<ColorTheme> SelectBuiltInThemeCommand { get; }
    public IRelayCommand<string> SelectCustomThemeCommand { get; }

    /// <summary>Re-reads the custom-theme list and current selection from configuration.
    /// Called by the window after the palette-editor dialog closes (and once from this ctor).</summary>
    public void RefreshFromConfiguration()
    {
        var names = _configuration.GetCustomThemeNames();

        CustomThemes.Clear();
        foreach (var name in names) CustomThemes.Add(new CustomThemeMenuItemViewModel(name));

        RefreshSelection();
    }

    // Body: the `menuItem.Tag is string themeName` branch of ThemeMenuItem_OnClick, verbatim,
    // with UpdateThemeMenuSelection(...) replaced by RefreshSelection() and App.ApplyTheme by _applyTheme.
    private void SelectCustomTheme(string? themeName)
    {
        if (themeName is null) return;   // RelayCommand<string> parameter guard (adaptation: replaces the Tag type-test)

        if (!_configuration.TryActivateTheme(themeName)) return;

        var selectedPalette = _configuration.GetThemePaletteColors();
        RefreshSelection();
        _applyTheme(_configuration.ColorTheme, selectedPalette.Count > 0 ? selectedPalette : null);
    }

    // Body: the `menuItem.Tag is not ColorTheme theme` branch, verbatim, same substitutions.
    private void SelectBuiltInTheme(ColorTheme theme)
    {
        var currentTheme = _configuration.ColorTheme;
        IReadOnlyDictionary<string, string>? paletteOverride = null;

        if (theme == ColorTheme.SurpriseMe) paletteOverride = SurprisePaletteGenerator.GenerateSurprisePalette();

        if (theme == currentTheme && paletteOverride is null)
        {
            RefreshSelection();
            return;
        }

        _configuration.SetColorTheme(theme, paletteOverride);
        var palette = _configuration.GetThemePaletteColors();
        RefreshSelection();
        _applyTheme(theme, palette.Count > 0 ? palette : null);
        _clearScrollViewerCache();
    }

    // Replaces UpdateThemeMenuSelection. IMPORTANT: raises PropertyChanged UNCONDITIONALLY.
    // A checkable MenuItem click writes IsChecked via SetCurrentValue; when the user clicks the
    // already-active theme the VM value doesn't change, so only an unconditional raise makes the
    // OneWay binding re-assert and keep the checkmark visible (mirrors the original handler's
    // "same theme → UpdateThemeMenuSelection and return" re-sync).
    private void RefreshSelection()
    {
        var theme = _configuration.ColorTheme;

        _isVintageStorySelected = theme == ColorTheme.VintageStory;
        _isDarkSelected = theme == ColorTheme.Dark;
        _isLightSelected = theme == ColorTheme.Light;
        OnPropertyChanged(nameof(IsVintageStorySelected));
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(IsLightSelected));

        var normalizedName = _configuration.GetCurrentThemeName();
        foreach (var item in CustomThemes)
            item.SetSelected(theme == ColorTheme.Custom
                             && !string.IsNullOrWhiteSpace(item.Name)
                             && string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>One entry in the custom-themes submenu.</summary>
public sealed class CustomThemeMenuItemViewModel : ObservableObject
{
    public CustomThemeMenuItemViewModel(string name) => Name = name;

    public string Name { get; }

    public bool IsSelected { get; private set; }

    // Unconditional raise, same rationale as ThemeMenuViewModel.RefreshSelection.
    internal void SetSelected(bool value)
    {
        IsSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }
}
```

(The original `UpdateThemeMenuSelection` compared the *menu item header* against the normalized name; here `item.Name` is that same header string — the custom items were created with `Header = name`. The original's `themeName ?? _userConfiguration.GetCurrentThemeName()` dance disappears because `RefreshSelection` always reads fresh from configuration — by the time the original passed a non-null `themeName`, config had already been updated to match, so reading config is equivalent. List this as an adaptation.)

**XAML rewires (`MainWindow.xaml:330–361`)** — the three built-in items lose `Click` and `Tag`, gain bindings (`services:` xmlns already exists in the file, it's used by the current `Tag={x:Static ...}`); pattern shown for Dark, apply identically for VintageStory and Light:

```xml
<MenuItem
    x:Name="DarkThemeMenuItem"
    Height="35"
    Command="{Binding ThemeMenu.SelectBuiltInThemeCommand, RelativeSource={RelativeSource AncestorType=Window}}"
    CommandParameter="{x:Static services:ColorTheme.Dark}"
    Header="_Dark"
    IsCheckable="True"
    IsChecked="{Binding ThemeMenu.IsDarkSelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}"
    Style="{StaticResource ToggleMenuItemStyle}" />
```

`ThemesMenuItem`, `CustomThemesSeparator`, and `EditThemeMenuItem` (still `Click="EditThemeMenuItem_OnClick"`) are unchanged.

**`MainWindow.Appearance.cs` after the rewire** — delete `ThemeMenuItem_OnClick` and `UpdateThemeMenuSelection`; keep `EditThemeMenuItem_OnClick` and a slimmed `RefreshCustomThemeMenuItems` + `ShowCustomThemeEditor`:

```csharp
private void RefreshCustomThemeMenuItems()
{
    if (ThemesMenuItem is null) return;

    foreach (var item in _customThemeMenuItems) ThemesMenuItem.Items.Remove(item);

    _customThemeMenuItems.Clear();

    ThemeMenu.RefreshFromConfiguration();

    if (ThemeMenu.CustomThemes.Count == 0)
    {
        if (CustomThemesSeparator is not null)
            CustomThemesSeparator.Visibility = Visibility.Collapsed;
        return;
    }

    if (CustomThemesSeparator is not null)
        CustomThemesSeparator.Visibility = Visibility.Visible;

    var toggleStyle = TryFindResource("ToggleMenuItemStyle") as Style;

    // Find the separator index
    var separatorIndex = CustomThemesSeparator is not null && ThemesMenuItem.Items.Contains(CustomThemesSeparator)
        ? ThemesMenuItem.Items.IndexOf(CustomThemesSeparator)
        : ThemesMenuItem.Items.Count;

    // Insert custom themes BEFORE the separator
    foreach (var themeItem in ThemeMenu.CustomThemes)
    {
        var menuItem = new MenuItem
        {
            Header = themeItem.Name,
            Height = 35,
            IsCheckable = true,
            Style = toggleStyle,
            Command = ThemeMenu.SelectCustomThemeCommand,
            CommandParameter = themeItem.Name
        };
        menuItem.SetBinding(MenuItem.IsCheckedProperty,
            new Binding(nameof(CustomThemeMenuItemViewModel.IsSelected)) { Source = themeItem, Mode = BindingMode.OneWay });

        _customThemeMenuItems.Add(menuItem);
        ThemesMenuItem.Items.Insert(separatorIndex, menuItem);
        separatorIndex++; // Increment to keep adding before the separator
    }
}

private void ShowCustomThemeEditor()
{
    ThemeMenu.RefreshFromConfiguration();

    var palette = _userConfiguration.GetThemePaletteColors();
    App.ApplyTheme(_userConfiguration.ColorTheme, palette.Count > 0 ? palette : null);
    ClearScrollViewerCache();

    var dialog = new ThemePaletteEditorDialog(_userConfiguration)
    {
        Owner = this
    };

    _ = dialog.ShowDialog();

    RefreshCustomThemeMenuItems();
    ClearScrollViewerCache();
}
```

(Needed usings in `Appearance.cs` after the edit: `System.Windows.Data` for `Binding`/`BindingMode`, `VintageStoryModManager.ViewModels` for the item-VM type — add; drop ones IDE0005 flags. `RefreshCustomThemeMenuItems` no longer unsubscribes `Click` because the new items never subscribe it. `ShowCustomThemeEditor`'s pre-dialog `UpdateThemeMenuSelection(currentTheme, currentThemeName)` and post-dialog `UpdateThemeMenuSelection(...)` both collapse into `RefreshFromConfiguration()` (pre-dialog, direct; post-dialog, via the `RefreshCustomThemeMenuItems` call which now refreshes the VM first) — list as adaptation. The `currentTheme`/`currentThemeName` locals become dead and are dropped.)

**`MainWindow.Core.cs`:**
1. Next to the slice-18 `SettingsMenu` init (before `InitializeComponent()`, after `_userConfiguration` exists):

```csharp
ThemeMenu = new ThemeMenuViewModel(
    _userConfiguration,
    (theme, palette) => App.ApplyTheme(theme, palette),
    ClearScrollViewerCache);
```

2. Property (next to `SettingsMenu`): `public ThemeMenuViewModel ThemeMenu { get; }`
3. Ctor line ~353 `UpdateThemeMenuSelection(_userConfiguration.ColorTheme, _userConfiguration.GetCurrentThemeName());` → **delete** (the preceding `RefreshCustomThemeMenuItems()` call at ~352 stays and now refreshes VM selection too).
4. `_customThemeMenuItems` field (line 304) stays — it's view state (the created `MenuItem`s).

**Specified adaptations (report all):** two typed commands replace the single Tag-dispatched handler (string branch → `SelectCustomTheme`, enum branch → `SelectBuiltInTheme`); `UpdateThemeMenuSelection` → `RefreshSelection` with unconditional `PropertyChanged` (SetCurrentValue re-assert rationale above) and always-read-from-config name resolution; `App.ApplyTheme`/`ClearScrollViewerCache` → injected delegates; dynamic items switch from `Click` to `Command` + per-item `IsChecked` binding; `ShowCustomThemeEditor` selection-sync calls → `RefreshFromConfiguration()`.

- [ ] **Step 1:** Grep inventory before editing: `grep -rn 'ThemeMenuItem_OnClick\|UpdateThemeMenuSelection\|RefreshCustomThemeMenuItems\|ShowCustomThemeEditor' VintageStoryModManager/ --include='*.cs' --include='*.xaml' | grep -v obj/` — expected callers: `Core.cs:352–353`, `Appearance.cs` internal, `MainWindow.xaml` `Click=` on the three built-ins. If any OTHER call site appears, stop and report.
- [ ] **Step 2:** Implement VM, XAML, Appearance/Core rewires.
- [ ] **Step 3:** Stale grep: `grep -n 'ThemeMenuItem_OnClick\|UpdateThemeMenuSelection' VintageStoryModManager/ -r --include='*.cs' --include='*.xaml' | grep -v obj/` → zero hits.
- [ ] **Step 4:** `dotnet build ./ImprovedModMenu.sln --configuration Release` → 0/0. Scoped IDE0005 on the four changed source files → clean.
- [ ] **Step 5:** Commit:

```bash
git add VintageStoryModManager/ViewModels/ThemeMenuViewModel.cs VintageStoryModManager/Views/MainWindow.xaml VintageStoryModManager/Views/MainWindow/MainWindow.Appearance.cs VintageStoryModManager/Views/MainWindow/MainWindow.Core.cs
git commit -m "refactor: bind theme menu to ThemeMenuViewModel (slice 19 part A, area K)"
```

---

### Task 3: ThemeMenuViewModel tests

**Files:**
- Create: `VintageStoryModManager.Tests/ThemeMenuViewModelTests.cs`

Reuse the extended `FakeUserConfiguration` shape from Task 1 (define a local copy in this file if the slice-18 fake is `private` to its test class — match whichever is cheaper; do NOT make the settings tests' fake public just for this). Recording delegates: a list capturing `(ColorTheme, IReadOnlyDictionary<string,string>?)` apply calls, a counter for scroll-cache clears.

Test list (~8):

1. `Ctor_SeedsSelectionAndCustomThemes` — fake with `ColorTheme = Dark`, two custom names: `IsDarkSelected` true, others false, `CustomThemes.Count == 2`, no apply calls.
2. `SelectBuiltIn_NewTheme_SetsConfig_Applies_ClearsCache` — from VintageStory select Dark: `SetColorThemeCalls` has `(Dark, null)`, one apply call with `Dark`, cache cleared once, `IsDarkSelected` true.
3. `SelectBuiltIn_SameTheme_NoConfigWrite_NoApply_ButRaisesPropertyChanged` — select the current theme: `SetColorThemeCalls` empty, zero apply calls, zero cache clears, AND a `PropertyChanged` for the theme's `Is*Selected` was raised anyway (subscribe and count — this pins the SetCurrentValue re-assert behavior).
4. `SelectBuiltIn_SurpriseMe_PassesGeneratedPalette` — select `SurpriseMe` while current: `SetColorThemeCalls` records a non-null palette override (surprise always re-applies even when `SurpriseMe` is already current, because `paletteOverride is null` is false — mirror the moved condition).
5. `SelectCustom_ActivatesByName_Applies_NoCacheClear` — custom "Ocean": `LastActivatedThemeName == "Ocean"`, one apply call, **zero cache clears** (the named branch never cleared it), item's `IsSelected` true.
6. `SelectCustom_ActivationFails_NoApply` — `TryActivateThemeResult = false`: zero apply calls, selection unchanged.
7. `SelectCustom_NullParameter_NoOp` — `SelectCustomThemeCommand.Execute(null)`: nothing recorded, no throw.
8. `RefreshFromConfiguration_RebuildsList_AndSelection` — mutate the fake's `CustomThemeNames` and `ColorTheme`/`CurrentThemeName` to a custom theme, call `RefreshFromConfiguration()`: list matches, matching item selected (case-insensitively — use a different casing).

- [ ] **Step 1:** Write; `dotnet test VintageStoryModManager.Tests --filter ThemeMenuViewModelTests` → all pass. (Known flake: `*_wpftmp` CS0103 from stale Debug obj — re-run once, then `dotnet clean ./ImprovedModMenu.sln --configuration Debug`.)
- [ ] **Step 2:** Commit:

```bash
git add VintageStoryModManager.Tests/ThemeMenuViewModelTests.cs
git commit -m "test: cover ThemeMenuViewModel (slice 19 part A, area K)"
```

---

### Task 4: Extract `InstalledModLogIdentifierListBuilder` (part B)

**Files:**
- Create: `VintageStoryModManager/Services/InstalledModLogIdentifierListBuilder.cs`
- Modify: `VintageStoryModManager/Views/MainWindow/MainWindow.Debugging.cs`
- Create: `VintageStoryModManager.Tests/InstalledModLogIdentifierListBuilderTests.cs`

**Interfaces — Produces** (loop body verbatim from `Debugging.cs:100–118`; `internal` matches `InstalledModLogIdentifier`'s visibility, tests reach it via the existing `InternalsVisibleTo`):

```csharp
namespace VintageStoryModManager.Services;

/// <summary>
///     Builds the deduplicated (mod-id, display-label) list the experimental log-debugging dialog
///     searches for. Pure input→output; twin of <see cref="InstalledModIdListBuilder" />.
/// </summary>
internal static class InstalledModLogIdentifierListBuilder
{
    internal static List<InstalledModLogIdentifier> Build(IEnumerable<(string? ModId, string? DisplayName)> mods)
    {
        var modIdentifiers = new List<InstalledModLogIdentifier>();
        var seenModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (modId, displayName) in mods)
        {
            if (string.IsNullOrWhiteSpace(modId)) continue;

            var trimmedModId = modId.Trim();
            if (!seenModIds.Add(trimmedModId)) continue;

            var trimmedDisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(trimmedDisplayName)) trimmedDisplayName = trimmedDisplayName.Trim();

            var displayLabel = string.IsNullOrWhiteSpace(trimmedDisplayName)
                ? trimmedModId
                : $"{trimmedDisplayName} ({trimmedModId})";

            modIdentifiers.Add(new InstalledModLogIdentifier(trimmedModId, displayLabel));
        }

        return modIdentifiers;
    }
}
```

**Call-site rewire in `ExperimentalAllModsDebuggingMenuItem_OnClick`** — lines 96–118 (`var installedMods = ...` stays; the `modIdentifiers`/`seenModIds` declarations and the whole `foreach` are replaced):

```csharp
var installedMods = _viewModel.GetInstalledModsSnapshot();
var modIdentifiers = InstalledModLogIdentifierListBuilder.Build(
    installedMods.Select(mod => (mod?.ModId, mod?.DisplayName)));
```

(The original's `if (mod is null) continue;` guard is preserved by the `mod?.` projections — a null mod becomes `(null, null)` and is skipped by the whitespace check. The original bound `displayName` to a new local before trimming; the moved body renames it `trimmedDisplayName` to keep the tuple deconstruction variable readonly — list both as adaptations. `System.Linq` is a project-wide implicit using.)

The single-mod handler (`ExperimentalModDebuggingMenuItem_OnClick`) is untouched.

**Test list (~5) for `InstalledModLogIdentifierListBuilderTests`:**
1. `Build_FormatsLabel_WithAndWithoutDisplayName` — `("smellyfeet", "Smelly Feet")` → label `"Smelly Feet (smellyfeet)"`; `("noname", null)` → label `"noname"`.
2. `Build_TrimsIdAndDisplayName` — `(" id ", "  Name  ")` → `SearchValue == "id"`, label `"Name (id)"`.
3. `Build_DedupesCaseInsensitively_FirstWins` — `("ModA", "First")` then `("moda", "Second")` → one entry, label `"First (ModA)"`.
4. `Build_SkipsNullAndWhitespaceIds` — `(null, "X")`, `("   ", "Y")`, `("ok", "Z")` → one entry.
5. `Build_WhitespaceDisplayName_FallsBackToId` — `("id", "   ")` → label `"id"`.

- [ ] **Step 1:** Create the builder; rewire the call site. Stale grep: `grep -n 'seenModIds' VintageStoryModManager/Views/MainWindow/MainWindow.Debugging.cs` → zero hits.
- [ ] **Step 2:** Build Release 0/0; write tests; `dotnet test VintageStoryModManager.Tests --filter InstalledModLogIdentifierListBuilderTests` → all pass.
- [ ] **Step 3:** Commit (extraction and tests may be one commit or two, matching part A's split if you prefer two):

```bash
git add VintageStoryModManager/Services/InstalledModLogIdentifierListBuilder.cs VintageStoryModManager/Views/MainWindow/MainWindow.Debugging.cs VintageStoryModManager.Tests/InstalledModLogIdentifierListBuilderTests.cs
git commit -m "refactor: extract installed-mod log-identifier builder from MainWindow.Debugging (slice 19 part B, area K)"
```

---

### Task 5: Final gates + report

- [ ] `dotnet build ./ImprovedModMenu.sln --configuration Release` → zero warnings/errors.
- [ ] Full suite: `dotnet test ./ImprovedModMenu.sln --configuration Release` → all pass (expected: prior count + ~13 new).
- [ ] Scoped IDE0005 on all changed files → clean.
- [ ] `git diff --name-only` off base: exactly the files listed in Tasks 1/2/3/4 — `MainViewModel.cs` must NOT appear.
- [ ] Report: worktree path + branch, commit hashes, full Adaptations list (parts A and B separately), test names/results, any deviations. No push; no `CLAUDE.md`/`.claude/`.

---

## Smoke test (orchestrator/user, post-merge) — XAML changed, so menu-heavy

Startup: current theme's checkmark is correct (built-in or custom). Click each built-in theme → applies visually, checkmark moves, previous unchecks. Click the **already-active** theme → stays checked (the SetCurrentValue re-assert case — this is the one to watch). Surprise Me → new palette every click, even twice in a row. If custom themes exist (or create one via Edit theme...): custom entries appear between Light and the separator, clicking one activates it (checkmark on it, built-ins uncheck), casing-insensitive match holds. Edit theme... → dialog opens; after closing, menu list and checkmarks reflect any created/renamed/deleted themes; scrolling in the mod grid still smooth (cache-clear path). Restart → theme persists, checkmark correct. Part B: select a mod → Mods menu → Experimental Mod Debugging still opens with log lines; Experimental All-Mods Debugging shows entries labeled "Name (modid)" with no duplicate mods.
