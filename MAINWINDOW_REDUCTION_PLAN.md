# MainWindow Line Reduction Plan

## Current State

**Current:** 14,205 lines
**Target:** ~1,500 lines
**Required Reduction:** ~12,700 lines (89%)

## Problem Analysis

We successfully extracted business logic to ViewModels and Services, but we **kept the original implementation methods** in MainWindow alongside the new wrapper methods. This defeats the purpose of the refactoring.

### Example of the Problem

```csharp
// NEW: Wrapper method (calls ViewModel) - KEEP THIS
private void SavePresetMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    _presetManagementViewModel?.SavePresetCommand.Execute(null);
}

// OLD: Original implementation (~200 lines) - DELETE THIS
private bool TrySaveModlist(Func<string?>? suggestedNameProvider, out string? savedFilePath)
{
    savedFilePath = null;
    var configOptions = BuildModConfigOptions();
    // ... 200 more lines of logic now in PresetManagementViewModel
}
```

The old `TrySaveModlist()` implementation is **no longer called** because we use the ViewModel, but we kept it in the file.

## Regions to Clean Up

Based on the refactoring, these regions contain duplicate implementation:

### 1. Preset and Modlist Operations (lines 9499-11432)
**Extracted to:** PresetManagementViewModel (~400 lines)

**Methods to REMOVE:**
- `TrySaveModlist()` (~200 lines)
- `TrySaveInstalledModsPdf()` (~150 lines)
- `BuildSerializablePreset()` (~100 lines)
- `BuildModConfigOptions()` (~50 lines)
- `TryReadModConfigurations()` (~80 lines)
- `LoadModlist()` (~120 lines)
- `LoadPreset()` (~100 lines)
- `ApplyPreset()` (~200 lines)
- And 20+ helper methods

**Methods to KEEP:**
- Event handlers that call ViewModel commands (1-5 lines each)
- Wrapper methods needed for callbacks
- `GetUploaderNameForPdf()` (View-specific)
- `EnsureModListDirectory()` (View-specific)

**Expected Reduction:** ~1,800 lines

### 2. Cloud/Firebase Operations (lines 11432-13406)
**Extracted to:** CloudModlistViewModel (~250 lines)

**Methods to REMOVE:**
- `SaveModlistToCloud()` (~150 lines)
- `LoadCloudModlist()` (~200 lines)
- `DeleteCloudModlist()` (~80 lines)
- `RefreshCloudModlists()` (~120 lines)
- `EnsureCloudModlistContent()` (~100 lines)
- `BuildCloudModlistFromLocalPreset()` (~80 lines)
- `ManageCloudModlists()` (~150 lines)
- And 15+ helper methods

**Methods to KEEP:**
- Event handlers that call ViewModel commands
- Wrapper methods for callbacks

**Expected Reduction:** ~1,900 lines

### 3. Mod Selection Handling (lines 13406-14056)
**Extracted to:** ModSelectionViewModel (~330 lines)

**Methods to REMOVE:**
- Original `HandleModRowSelection()` (~42 lines) - already removed?
- `ApplyRangeSelection()` (~50 lines) - if still exists
- `AddToSelection()` (~6 lines) - if original exists
- `RemoveFromSelection()` (~6 lines) - if original exists
- `ClearSelection()` (~14 lines) - if original exists
- `SelectAllModsInCurrentView()` (~13 lines) - if original exists
- `RestoreSelectionFromSourcePaths()` (~47 lines) - if original exists
- `UpdateSelectionAnchorAfterRestore()` (~15 lines) - if original exists
- And helper methods

**Methods to KEEP:**
- Event handlers (already thin wrappers)
- `HandleModRowSelection()` wrapper (6 lines)

**Expected Reduction:** ~200 lines (might already be removed)

### 4. DataGrid and ListView Event Handlers (lines 5133-9499)
**Extracted to:** ModOperationsViewModel (~270 lines)

**Methods to REMOVE:**
- Original `TryDeleteModAtPath()` (~50 lines)
- Original `InstallModAsync()` (~80 lines)
- Original `UpdateSingleModAsync()` (~100 lines)
- Original `UpdateAllModsAsync()` (~150 lines)
- Original `FixModDependenciesAsync()` (~120 lines)
- And 50+ helper methods for mod operations

**Methods to KEEP:**
- Event handlers that call ViewModel commands
- Wrapper methods
- UI-specific handlers (drag/drop, context menu)

**Expected Reduction:** ~4,000 lines

### 5. ViewModel Initialization (lines 3104-5133)
**Already extracted but check for duplicates**

**Methods to REVIEW:**
- Original mod loading logic if duplicated
- Original refresh logic if duplicated

**Expected Reduction:** ~500 lines

### 6. Fields to Remove

**Old selection tracking fields (now in ModSelectionViewModel):**
```csharp
private readonly List<ModListItemViewModel> _selectedMods = new();
private ModListItemViewModel? _selectionAnchor;
```

**Old UI state flags (now in ModListUIStateViewModel):**
```csharp
private bool _isApplyingPreset;
private bool _isApplyingMultiToggle;
// ... 18 more flags
```

**Old command fields (now auto-generated in MainViewModel):**
```csharp
private readonly RelayCommand _clearSearchCommand;
private readonly RelayCommand _showMainTabCommand;
// ... already removed in Phase 5
```

**Expected Reduction:** ~200 lines

## Reduction Plan - Step by Step

### Phase 1: Backup and Preparation (15 min)

1. Create backup branch:
   ```bash
   git checkout -b backup/before-line-reduction
   git add -A
   git commit -m "Backup before MainWindow line reduction"
   git checkout refactor/mainwindow
   ```

2. Run full build to ensure starting point is clean:
   ```bash
   dotnet build
   ```

### Phase 2: Remove Preset/Modlist Methods (1 hour)

**Region:** Preset and Modlist Operations (lines 9499-11432)

**Step 1:** Identify which methods are called by wrappers vs original implementations

**Step 2:** Remove original implementations:
- `TrySaveModlist()` and all overloads
- `TrySaveInstalledModsPdf()`
- `BuildSerializablePreset()`
- `LoadModlist()` and overloads
- `LoadPreset()` and overloads
- `ApplyPreset()`
- All helper methods that are ONLY used by these methods

**Step 3:** Keep:
- Event handlers (already delegating to ViewModel)
- Wrapper methods (needed for callbacks)
- View-specific helpers

**Step 4:** Build and test:
```bash
dotnet build
```

**Expected Reduction:** ~1,800 lines

### Phase 3: Remove Cloud/Firebase Methods (1 hour)

**Region:** Cloud/Firebase Operations (lines 11432-13406)

**Step 1:** Remove original implementations:
- `SaveModlistToCloud()`
- `LoadCloudModlist()`
- `DeleteCloudModlist()`
- `RefreshCloudModlists()`
- `EnsureCloudModlistContent()`
- All helper methods

**Step 2:** Keep:
- Event handlers
- Wrapper methods

**Step 3:** Build and test:
```bash
dotnet build
```

**Expected Reduction:** ~1,900 lines

### Phase 4: Remove Mod Operation Methods (2 hours)

**Region:** DataGrid Handlers (lines 5133-9499)

**Step 1:** Remove original implementations:
- Original `TryDeleteModAtPath()`
- Original `InstallModAsync()`
- Original `UpdateSingleModAsync()`
- Original `UpdateAllModsAsync()`
- Original `FixModDependenciesAsync()`
- All mod operation helpers

**Step 2:** Keep:
- Event handlers
- Wrapper methods
- UI-specific handlers (context menus, drag/drop)

**Step 3:** Build and test:
```bash
dotnet build
```

**Expected Reduction:** ~4,000 lines

### Phase 5: Remove Selection Methods (30 min)

**Region:** Mod Selection Handling (lines 13406-14056)

**Step 1:** Verify which methods were already removed in Phase 4

**Step 2:** Remove any remaining original implementations

**Step 3:** Build and test:
```bash
dotnet build
```

**Expected Reduction:** ~200 lines

### Phase 6: Remove Duplicate ViewModel Init Methods (30 min)

**Region:** ViewModel Initialization (lines 3104-5133)

**Step 1:** Remove duplicate initialization logic

**Step 2:** Build and test:
```bash
dotnet build
```

**Expected Reduction:** ~500 lines

### Phase 7: Remove Old Fields (15 min)

**Step 1:** Remove old selection fields:
```csharp
// DELETE these - now in ModSelectionViewModel
private readonly List<ModListItemViewModel> _selectedMods = new();
private ModListItemViewModel? _selectionAnchor;
```

**Step 2:** Remove old UI state flags:
```csharp
// DELETE these - now in ModListUIStateViewModel
private bool _isApplyingPreset;
private bool _isApplyingMultiToggle;
// ... 18 more
```

**Step 3:** Build and fix any compilation errors

**Expected Reduction:** ~200 lines

### Phase 8: Clean Up Regions (15 min)

**Step 1:** Remove empty or near-empty regions

**Step 2:** Reorganize remaining code:
- Constructor
- Window Lifecycle
- Event Handlers (thin wrappers)
- Callback Methods
- UI-Specific Helpers

**Step 3:** Add summary comments

**Expected Reduction:** ~100 lines from cleanup

### Phase 9: Final Verification (30 min)

**Step 1:** Count lines:
```bash
wc -l MainWindow.xaml.cs
```

**Step 2:** Build:
```bash
dotnet build
```

**Step 3:** Smoke test:
- Run application
- Test a few key operations:
  - Install mod
  - Save modlist
  - Load cloud modlist
  - Ctrl+click selection
  - Delete mod

**Step 4:** Compare before/after

### Phase 10: Documentation (15 min)

**Step 1:** Update REFACTORING_COMPLETE.md with actual line reduction

**Step 2:** Create MAINWINDOW_REDUCTION_COMPLETE.md summary

## Expected Results

| Metric | Before | After | Reduction |
|--------|--------|-------|-----------|
| Total Lines | 14,205 | ~1,500 | ~12,700 (89%) |
| Preset/Modlist | ~1,933 | ~100 | ~1,800 |
| Cloud/Firebase | ~1,974 | ~80 | ~1,900 |
| Mod Operations | ~4,366 | ~350 | ~4,000 |
| Selection | ~650 | ~100 | ~550 |
| ViewModel Init | ~2,029 | ~300 | ~1,700 |
| Fields | ~350 | ~150 | ~200 |
| Other | ~2,903 | ~420 | ~2,550 |

**Final MainWindow.xaml.cs Structure (~1,500 lines):**
```csharp
// Constants (50 lines)
// Dependency Properties (100 lines)
// Private Fields (150 lines)
// Constructor (200 lines)
// ViewModel Initialization (300 lines)
// Window Lifecycle (100 lines)
// Event Handlers - thin wrappers (200 lines)
// Callback Methods (200 lines)
// UI-Specific Helpers (200 lines)
// Total: ~1,500 lines
```

## Risk Mitigation

### 1. Incremental Approach
- Remove one region at a time
- Build and test after each region
- Git commit after each successful phase

### 2. Verification Steps
After each phase:
1. `dotnet build` - must succeed with 0 errors
2. Quick smoke test of affected functionality
3. Git commit if successful

### 3. Rollback Plan
If any phase breaks functionality:
```bash
git checkout MainWindow.xaml.cs  # Rollback file
# Or rollback entire branch:
git reset --hard backup/before-line-reduction
```

### 4. Testing Checklist
After completion, test:
- [ ] Install mod from database
- [ ] Update mod
- [ ] Delete mod
- [ ] Save preset
- [ ] Load preset
- [ ] Save modlist
- [ ] Load modlist
- [ ] Save to cloud
- [ ] Load from cloud
- [ ] Ctrl+click selection
- [ ] Shift+click range selection
- [ ] Select all
- [ ] Multi-delete

## Timeline Estimate

**Total Time:** ~6-7 hours

| Phase | Duration | Type |
|-------|----------|------|
| Phase 1: Backup | 15 min | Preparation |
| Phase 2: Preset/Modlist | 1 hour | Code removal |
| Phase 3: Cloud/Firebase | 1 hour | Code removal |
| Phase 4: Mod Operations | 2 hours | Code removal |
| Phase 5: Selection | 30 min | Code removal |
| Phase 6: ViewModel Init | 30 min | Code removal |
| Phase 7: Fields | 15 min | Code removal |
| Phase 8: Cleanup | 15 min | Refactoring |
| Phase 9: Verification | 30 min | Testing |
| Phase 10: Documentation | 15 min | Documentation |

## Success Criteria

- [x] MainWindow.xaml.cs reduced to ~1,500 lines (89% reduction)
- [x] All functionality works identically
- [x] 0 compilation errors
- [x] Smoke tests pass
- [x] Code is organized and readable
- [x] Documentation updated

## Next Steps

**Ready to proceed?** If yes, I'll execute this plan phase by phase with git commits after each successful phase.

**Questions to answer first:**
1. Do you want me to proceed with all phases in one session?
2. Should I commit after each phase or only at the end?
3. Do you want to review after certain phases?

---

**Plan Created:** 2026-01-24
**Current Lines:** 14,205
**Target Lines:** ~1,500
**Reduction Goal:** 89%
