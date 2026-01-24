# Phase 1: Foundation Services - Review Document

## Overview

Phase 1 successfully extracted domain logic from MainWindow.xaml.cs into 4 new services, reducing coupling and improving testability. All services compile successfully with zero warnings.

---

## 1. ModListService

**Location:** `VintageStoryModManager/Services/ModListService.cs` (450 lines)

**Purpose:** Handles all preset and modlist serialization, deserialization, and PDF generation.

### Extracted From MainWindow
- `BuildSerializablePreset()` (lines 9289-9352)
- `BuildSerializableConfigList()` (lines 9362-9410)
- `TrySaveModlist()` logic (lines 9436-9539)
- `TryBuildCurrentModlistJson()` (lines 9665-9693)
- `TrySaveInstalledModsPdf()` (lines 6141-6169)
- `GenerateInstalledModsPdf()` (lines 9938-10050)
- Helper methods for config file handling

### Key Methods

```csharp
// Build a serializable preset from mod states
SerializablePreset BuildSerializablePreset(
    string entryName,
    IReadOnlyList<ModPresetModState> modStates,
    bool includeModVersions,
    bool exclusive,
    IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations = null,
    string? gameVersion = null)

// Save preset to JSON file
bool SavePresetToFile(string filePath, SerializablePreset preset)

// Load preset from JSON file
bool LoadPresetFromFile(string filePath, out SerializablePreset? preset, out string? errorMessage)

// Build modlist JSON string
bool TryBuildModlistJson(
    string modlistName,
    IReadOnlyList<ModPresetModState> modStates,
    string? description,
    string? version,
    string uploader,
    IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
    string? gameVersion,
    out string json)

// Generate PDF from modlist
bool GeneratePdf(
    string filePath,
    string listName,
    string? modlistVersion,
    string? description,
    string uploaderName,
    string? gameVersion,
    IReadOnlyList<ModListItemViewModel> mods,
    IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
    IReadOnlyList<ModPresetModState> modStates)
```

### Benefits
- ✅ **No UI dependencies** - Pure business logic
- ✅ **Fully testable** - Can mock file I/O, test serialization logic
- ✅ **Reusable** - Can be used by ViewModels, dialogs, or CLI tools
- ✅ **Single Responsibility** - Only handles modlist serialization

### Testing Recommendations

```csharp
[Test]
public void BuildSerializablePreset_WithValidStates_ReturnsPreset()
{
    // Arrange
    var service = new ModListService();
    var states = new List<ModPresetModState>
    {
        new() { ModId = "game.testmod", Version = "1.0.0", IsActive = true }
    };

    // Act
    var preset = service.BuildSerializablePreset("TestPreset", states, true, true);

    // Assert
    Assert.That(preset.Name, Is.EqualTo("TestPreset"));
    Assert.That(preset.Mods, Has.Count.EqualTo(1));
    Assert.That(preset.IncludeModVersions, Is.True);
}

[Test]
public void SavePresetToFile_WithValidPreset_ReturnsTrue()
{
    // Arrange
    var service = new ModListService();
    var preset = new SerializablePreset { Name = "Test", Mods = new List<SerializablePresetModState>() };
    var tempFile = Path.GetTempFileName();

    try
    {
        // Act
        var result = service.SavePresetToFile(tempFile, preset);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(File.Exists(tempFile), Is.True);
    }
    finally
    {
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }
}
```

---

## 2. FirebaseModlistService

**Location:** `VintageStoryModManager/Services/FirebaseModlistService.cs` (180 lines)

**Purpose:** Wraps `FirebaseModlistStore` to provide simplified, thread-safe initialization and a cleaner API for cloud operations.

### Extracted From MainWindow
- `EnsureCloudStoreInitializedAsync()` (lines 11556-11586)
- Cloud store initialization locking logic
- Player identity management

### Key Methods

```csharp
// Initialize with player identity
Task<FirebaseModlistStore> InitializeAsync(
    string? playerUid,
    string? playerName,
    CancellationToken cancellationToken = default)

// Get initialized store (throws if not initialized)
FirebaseModlistStore GetStore()

// Save modlist to cloud slot
Task SaveModlistAsync(string slotKey, string modlistJson, CancellationToken cancellationToken = default)

// Load modlist from cloud slot
Task<string?> LoadModlistAsync(string slotKey, CancellationToken cancellationToken = default)

// Delete modlist from cloud slot
Task DeleteModlistAsync(string slotKey, CancellationToken cancellationToken = default)

// List occupied slots
Task<IReadOnlyList<string>> ListSlotsAsync(CancellationToken cancellationToken = default)

// Get public registry entries
Task<IReadOnlyList<CloudModlistRegistryEntry>> GetRegistryEntriesAsync(
    CancellationToken cancellationToken = default)
```

### Benefits
- ✅ **Thread-safe initialization** - Uses semaphore to prevent race conditions
- ✅ **Simplified API** - Hides `FirebaseModlistStore` complexity
- ✅ **Single initialization** - Player identity set once, reused across calls
- ✅ **Proper disposal** - Implements IDisposable pattern

### Testing Recommendations

```csharp
[Test]
public async Task InitializeAsync_WithValidIdentity_ReturnsStore()
{
    // Arrange
    var service = new FirebaseModlistService();

    // Act
    var store = await service.InitializeAsync("test-uid", "TestPlayer");

    // Assert
    Assert.That(store, Is.Not.Null);
    Assert.That(service.GetCurrentUserId(), Is.EqualTo("test-uid"));
}

[Test]
public async Task InitializeAsync_CalledTwice_ReturnsSameStore()
{
    // Arrange
    var service = new FirebaseModlistService();

    // Act
    var store1 = await service.InitializeAsync("test-uid", "TestPlayer");
    var store2 = await service.InitializeAsync("test-uid", "TestPlayer");

    // Assert
    Assert.That(store1, Is.SameAs(store2));
}

[Test]
public void GetStore_WhenNotInitialized_ThrowsInvalidOperationException()
{
    // Arrange
    var service = new FirebaseModlistService();

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => service.GetStore());
}
```

---

## 3. ModSelectionService

**Location:** `VintageStoryModManager/Services/ModSelectionService.cs` (200 lines)

**Purpose:** Contains pure selection algorithm logic for Ctrl/Shift/range selection without any UI dependencies.

### Extracted From MainWindow
- `HandleModRowSelection()` (lines 13427-13470)
- `ApplyRangeSelection()` (lines 13486-13507)
- Selection toggle logic
- Select all logic

### Key Methods

```csharp
// Main selection calculation with keyboard modifiers
SelectionResult CalculateNewSelection(
    int clickedIndex,
    IReadOnlyList<int> currentSelection,
    int? anchorIndex,
    bool isShiftPressed,
    bool isCtrlPressed,
    int viewSize)

// Calculate range selection (Shift+Click)
IReadOnlyList<int> CalculateRangeSelection(
    int anchorIndex,
    int targetIndex,
    IReadOnlyList<int> currentSelection,
    bool preserveExisting)

// Calculate toggle selection (Ctrl+Click)
IReadOnlyList<int> CalculateToggleSelection(
    int index,
    IReadOnlyList<int> currentSelection)

// Calculate select all
IReadOnlyList<int> CalculateSelectAll(int viewSize)

// Resolve items from indices
IReadOnlyList<T> ResolveSelection<T>(
    IReadOnlyList<T> items,
    IReadOnlyList<int> selectedIndices)

// Find indices of items
IReadOnlyList<int> FindIndices<T>(
    IReadOnlyList<T> items,
    IReadOnlyList<T> selectedItems)
```

### Benefits
- ✅ **Zero UI dependencies** - Pure business logic, completely testable
- ✅ **Consistent behavior** - Same selection logic can be used across different UI controls
- ✅ **Easy to test edge cases** - Can test empty lists, out-of-bounds, etc.
- ✅ **Generic helpers** - Can work with any item type

### Testing Recommendations

```csharp
[Test]
public void CalculateNewSelection_NormalClick_SelectsSingleItem()
{
    // Arrange
    var service = new ModSelectionService();
    var currentSelection = new List<int> { 0, 1 };

    // Act
    var result = service.CalculateNewSelection(
        clickedIndex: 2,
        currentSelection: currentSelection,
        anchorIndex: 1,
        isShiftPressed: false,
        isCtrlPressed: false,
        viewSize: 10);

    // Assert
    Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 2 }));
    Assert.That(result.NewAnchorIndex, Is.EqualTo(2));
}

[Test]
public void CalculateNewSelection_CtrlClick_TogglesSelection()
{
    // Arrange
    var service = new ModSelectionService();
    var currentSelection = new List<int> { 0, 2 };

    // Act - Click on already selected item with Ctrl
    var result = service.CalculateNewSelection(
        clickedIndex: 2,
        currentSelection: currentSelection,
        anchorIndex: 0,
        isShiftPressed: false,
        isCtrlPressed: true,
        viewSize: 10);

    // Assert - Item 2 should be removed
    Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 0 }));
}

[Test]
public void CalculateNewSelection_ShiftClick_SelectsRange()
{
    // Arrange
    var service = new ModSelectionService();
    var currentSelection = new List<int> { 2 };

    // Act - Shift+Click from anchor 2 to index 5
    var result = service.CalculateNewSelection(
        clickedIndex: 5,
        currentSelection: currentSelection,
        anchorIndex: 2,
        isShiftPressed: true,
        isCtrlPressed: false,
        viewSize: 10);

    // Assert - Should select 2, 3, 4, 5
    Assert.That(result.SelectedIndices, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
    Assert.That(result.NewAnchorIndex, Is.EqualTo(2)); // Anchor stays at 2
}

[Test]
public void CalculateRangeSelection_WithReverseRange_SelectsCorrectly()
{
    // Arrange
    var service = new ModSelectionService();

    // Act - Range from 5 to 2 (backwards)
    var result = service.CalculateRangeSelection(5, 2, Array.Empty<int>(), false);

    // Assert - Should select 2, 3, 4, 5 (normalized)
    Assert.That(result, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
}
```

---

## 4. DialogService

**Location:** `VintageStoryModManager/Services/DialogService.cs` (200 lines)

**Purpose:** Centralizes dialog creation for consistent, testable dialog interactions.

### Extracted From MainWindow
- Dialog creation patterns scattered throughout MainWindow
- Common confirmation/error/info/warning message boxes

### Key Methods

```csharp
// Show save modlist dialog
SaveModlistDialogResult? ShowSaveModlistDialog(
    Window owner,
    string? suggestedName,
    List<ModConfigOption> configOptions,
    string uploaderName,
    string? defaultGameVersion)

// Show cloud modlist details dialog
CloudModlistDetailsDialogResult? ShowCloudModlistDetailsDialog(
    Window owner,
    string suggestedName,
    List<ModConfigOption> configOptions,
    string? defaultGameVersion)

// Show cloud slot selection dialog
CloudModlistSlot? ShowCloudSlotSelectionDialog(
    Window owner,
    IReadOnlyList<CloudModlistSlot> slots,
    string title,
    string message)

// Common message boxes
MessageBoxResult ShowConfirmation(Window? owner, string message, string title, ...)
void ShowError(Window? owner, string message, string title = "Simple VS Manager")
void ShowInformation(Window? owner, string message, string title = "Simple VS Manager")
void ShowWarning(Window? owner, string message, string title = "Simple VS Manager")
```

### Benefits
- ✅ **Centralized dialog logic** - Easier to mock in tests
- ✅ **Type-safe results** - Uses record types instead of dynamic dialog properties
- ✅ **Consistent API** - All dialogs follow same pattern
- ✅ **Easy to test ViewModels** - Can inject mock DialogService

### Testing Recommendations

```csharp
// For unit testing ViewModels with DialogService
public class MockDialogService : DialogService
{
    public SaveModlistDialogResult? MockSaveModlistResult { get; set; }

    public override SaveModlistDialogResult? ShowSaveModlistDialog(...)
    {
        return MockSaveModlistResult;
    }
}

[Test]
public async Task SaveModlist_WhenUserCancels_DoesNotSave()
{
    // Arrange
    var mockDialog = new MockDialogService { MockSaveModlistResult = null };
    var viewModel = new PresetManagementViewModel(mockDialog, ...);

    // Act
    var result = await viewModel.SaveModlistCommand.ExecuteAsync(null);

    // Assert
    Assert.That(result, Is.False); // Save was cancelled
}
```

---

## Impact Analysis

### Lines Extracted from MainWindow.xaml.cs
Based on the plan, these regions will eventually be extracted:
- **Preset/Modlist Operations** (~2,038 lines) → ModListService + PresetManagementViewModel
- **Cloud Operations** (~1,971 lines) → FirebaseModlistService + CloudModlistViewModel
- **Selection Logic** (~200 lines) → ModSelectionService + ModSelectionViewModel
- **Dialog Creation** (scattered) → DialogService

### Current MainWindow State
- **Before Phase 1:** 14,440 lines
- **After Phase 1:** Still 14,440 lines (services created but not yet integrated)
- **Phase 1 Status:** ✅ Foundation ready for ViewModel extraction

### Code Quality Improvements
1. **Testability** ⬆️ - Services have zero UI dependencies
2. **Separation of Concerns** ⬆️ - Domain logic separated from presentation
3. **Reusability** ⬆️ - Services can be used by multiple ViewModels
4. **Maintainability** ⬆️ - Smaller, focused classes vs. 14K line monolith

---

## Integration Plan (Preview)

Here's how these services will be used in Phase 2-3:

```csharp
// PresetManagementViewModel (Phase 3)
public partial class PresetManagementViewModel : ObservableObject
{
    private readonly ModListService _modListService;
    private readonly DialogService _dialogService;
    private readonly UserConfigurationService _userConfiguration;

    public PresetManagementViewModel(
        ModListService modListService,
        DialogService dialogService,
        UserConfigurationService userConfiguration)
    {
        _modListService = modListService;
        _dialogService = dialogService;
        _userConfiguration = userConfiguration;
    }

    [RelayCommand]
    private async Task SaveModlistAsync()
    {
        var configOptions = BuildModConfigOptions();
        var uploaderName = GetUploaderName();

        var result = _dialogService.ShowSaveModlistDialog(
            owner: _owner,
            suggestedName: null,
            configOptions: configOptions,
            uploaderName: uploaderName,
            defaultGameVersion: _installedGameVersion);

        if (result is null) return; // User cancelled

        if (result.SelectedAction == SaveInstalledModsDialogResult.SavePdf)
        {
            var mods = GetInstalledMods();
            var modStates = GetModStates();
            var success = _modListService.GeneratePdf(
                filePath: GetPdfPath(result.ListName),
                listName: result.ListName,
                modlistVersion: result.Version,
                description: result.Description,
                uploaderName: result.CreatedBy,
                gameVersion: result.VintageStoryVersion,
                mods: mods,
                includedConfigurations: ReadConfigurations(result.SelectedConfigOptions),
                modStates: modStates);

            if (success)
                ReportStatus($"Saved PDF for {result.ListName}");
        }
        else
        {
            var modStates = GetModStates();
            var json = _modListService.TryBuildModlistJson(...);
            // ... save to file
        }
    }
}
```

---

## Recommendations for Review

### 1. Code Review Checklist
- [ ] Review ModListService for correct serialization logic
- [ ] Verify FirebaseModlistService handles thread safety correctly
- [ ] Check ModSelectionService algorithm correctness (especially edge cases)
- [ ] Ensure DialogService has all needed dialog methods

### 2. Manual Testing (Optional)
Since these are services without UI integration yet, you can:
- Create a simple console app to test ModListService serialization
- Use unit tests (recommended) to verify selection algorithms
- Review the code to ensure business logic is correct

### 3. Questions to Consider
- Does ModListService handle all modlist formats correctly?
- Is the Firebase initialization pattern acceptable?
- Are there any edge cases in selection logic we should test?
- Should DialogService have additional helper methods?

### 4. Next Phase Preview
Once approved, Phase 2 will create:
- **DataBackupViewModel** - Extract backup UI logic
- **ModOperationsViewModel** - Extract mod install/update/delete logic
- MainWindow integration will begin (connecting ViewModels to UI)

---

## Summary

✅ **Phase 1 Complete**
- 4 services created (~1,030 lines of new, testable code)
- Zero compilation warnings or errors
- No breaking changes to existing code
- Foundation ready for ViewModel extraction in Phases 2-6

**What's Next?**
Review this document, test the services if desired, and provide feedback. Once approved, we'll proceed to Phase 2: Operations ViewModels.
