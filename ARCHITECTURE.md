# VintageStory Mod Manager - Architecture Documentation

## Overview

This document describes the current architecture after the MVVM refactoring project. The application follows a clean MVVM (Model-View-ViewModel) pattern with a service layer for business logic.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              MainWindow.xaml.cs (View)                   │  │
│  │  • Window lifecycle management                           │  │
│  │  • Event wiring (thin delegates to ViewModels)           │  │
│  │  • UI-specific operations (drag panel, scrolling)        │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Binds to                                      │
│                 ▼                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    ViewModels                             │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │                                                           │  │
│  │  MainViewModel (modernized)                              │  │
│  │  • Mod list coordination                                 │  │
│  │  • Search/filter/sort                                    │  │
│  │  • Tab navigation                                        │  │
│  │  • Observable properties with [ObservableProperty]       │  │
│  │  • Commands with [RelayCommand]                          │  │
│  │                                                           │  │
│  │  DataBackupViewModel                                     │  │
│  │  • VintagestoryData backup management                    │  │
│  │  • Automatic backup before operations                    │  │
│  │                                                           │  │
│  │  ModOperationsViewModel                                  │  │
│  │  • Install/Update/Delete/Fix operations                  │  │
│  │  • Progress tracking                                     │  │
│  │                                                           │  │
│  │  PresetManagementViewModel                               │  │
│  │  • Preset save/load                                      │  │
│  │  • Modlist save/load (JSON/PDF)                          │  │
│  │                                                           │  │
│  │  CloudModlistViewModel                                   │  │
│  │  • Firebase cloud storage                                │  │
│  │  • Upload/Download/Delete modlists                       │  │
│  │                                                           │  │
│  │  ModSelectionViewModel                                   │  │
│  │  • Custom multi-select (Ctrl/Shift/Range)                │  │
│  │  • Selection preservation                                │  │
│  │                                                           │  │
│  │  ModListUIStateViewModel                                 │  │
│  │  • Centralized UI state flags                            │  │
│  │  • Operation blocking logic                              │  │
│  │                                                           │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Calls                                         │
└─────────────────┼─────────────────────────────────────────────┘
                  │
┌─────────────────┼─────────────────────────────────────────────┐
│                 ▼             SERVICE LAYER                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                      Services                             │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │                                                           │  │
│  │  ModListService                                          │  │
│  │  • BuildPresetAsync()                                    │  │
│  │  • LoadPresetAsync()                                     │  │
│  │  • SavePresetAsync()                                     │  │
│  │  • GeneratePdfAsync()                                    │  │
│  │                                                           │  │
│  │  FirebaseModlistService                                  │  │
│  │  • GetModlistsAsync()                                    │  │
│  │  • SaveModlistAsync()                                    │  │
│  │  • DeleteModlistAsync()                                  │  │
│  │                                                           │  │
│  │  ModInstallService                                       │  │
│  │  • InstallModAsync()                                     │  │
│  │  • TryFixModAsync()                                      │  │
│  │  • DownloadModAsync()                                    │  │
│  │                                                           │  │
│  │  ModSelectionService                                     │  │
│  │  • CalculateNewSelection()                               │  │
│  │  • CalculateRangeSelection()                             │  │
│  │                                                           │  │
│  │  DialogService                                           │  │
│  │  • ShowSaveModlistDialogAsync()                          │  │
│  │  • ShowConfirmationAsync()                               │  │
│  │                                                           │  │
│  │  + Existing Services:                                    │  │
│  │  • ModDiscoveryService                                   │  │
│  │  • ModDatabaseService                                    │  │
│  │  • UserConfigurationService                              │  │
│  │  • DataBackupService                                     │  │
│  │  • ModUpdateService                                      │  │
│  │                                                           │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Operates on                                   │
└─────────────────┼─────────────────────────────────────────────┘
                  │
┌─────────────────┼─────────────────────────────────────────────┐
│                 ▼              MODEL LAYER                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                      Models                               │  │
│  ├──────────────────────────────────────────────────────────┤  │
│  │                                                           │  │
│  │  ModListItemViewModel                                    │  │
│  │  • Individual mod representation                         │  │
│  │  • Observable properties                                 │  │
│  │                                                           │  │
│  │  ModEntry                                                │  │
│  │  • Mod metadata                                          │  │
│  │                                                           │  │
│  │  CloudModlistListEntry                                   │  │
│  │  • Cloud modlist metadata                                │  │
│  │                                                           │  │
│  │  LocalModlistListEntry                                   │  │
│  │  • Local modlist metadata                                │  │
│  │                                                           │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### View Layer (MainWindow.xaml.cs)

**Responsibilities:**
- Window lifecycle (Loaded, Closing, sizing)
- Event wiring (delegates to ViewModel commands)
- View-specific UI (drag panel, scrolling)
- Exception boundaries

**Does NOT contain:**
- Business logic
- Complex state management
- Data transformation

**Example:**
```csharp
private async void InstallCloudModlistButton_OnClick(object sender, RoutedEventArgs e)
{
    if (_cloudModlistViewModel?.SelectedCloudModlist is null) return;
    await _cloudModlistViewModel.LoadFromCloudCommand.ExecuteAsync(_cloudModlistViewModel.SelectedCloudModlist);
}
```

### ViewModel Layer

#### MainViewModel
**Purpose:** Coordinates mod list display, search, filter, sort, and tab navigation

**Key Properties:**
- `ModsView` (ICollectionView) - Filtered/sorted mod list
- `SearchText` (string) - Search query
- `SelectedSortOption` (SortOption) - Current sort
- `IsBusy` (bool) - Loading state
- `TotalMods` (int) - Mod count
- `SelectedMod` (ModListItemViewModel) - Current selection

**Key Commands:**
- `RefreshCommand` - Reload mod list
- `ClearSearchCommand` - Clear search
- `ShowMainTabCommand` - Navigate to main tab
- `ShowDatabaseTabCommand` - Navigate to database tab
- `ShowModlistTabCommand` - Navigate to modlist tab

**Pattern:** MVVM Toolkit with [ObservableProperty] and [RelayCommand]

#### DataBackupViewModel
**Purpose:** Manages VintagestoryData folder backup/restore operations

**Key Properties:**
- `IsDataBackupInProgress` (bool)
- `DataBackupProgress` (double)
- `DataBackupStatusMessage` (string)

**Key Commands:**
- `CreateDataBackupCommand` - Create manual backup
- `RestoreBackupCommand` - Restore from backup
- `DeleteBackupsCommand` - Delete old backups

**Dependencies:** DataBackupService, UserConfigurationService

#### ModOperationsViewModel
**Purpose:** Handles mod install/update/delete/fix operations

**Key Properties:**
- `IsModUpdateInProgress` (bool)
- `ModUpdateProgress` (double)
- `UpdatableModsCount` (int)

**Key Commands:**
- `DeleteModCommand` - Delete single mod
- `DeleteMultipleModsCommand` - Delete multiple mods
- `InstallModCommand` - Install mod from database
- `UpdateModCommand` - Update single mod
- `UpdateAllModsCommand` - Update all mods
- `FixModDependenciesCommand` - Fix mod dependencies

**Callbacks:**
- `OnDeleteMod` - Delete mod from MainWindow
- `OnRequestRefreshAsync` - Refresh mod list
- `OnRequestAutomaticBackupAsync` - Create backup before operation
- `OnReportStatus` - Report status message

**Dependencies:** ModInstallService, ModUpdateService, ModDatabaseService

#### PresetManagementViewModel
**Purpose:** Manages preset and modlist save/load operations (JSON/PDF)

**Key Properties:**
- `IsSavingModlist` (bool)
- `SaveProgress` (double)
- `SaveStatusMessage` (string)

**Key Commands:**
- `SavePresetCommand` - Save current mods as preset
- `SaveModlistCommand` - Save modlist with metadata
- `LoadPresetCommand` - Load preset
- `LoadModlistCommand` - Load modlist
- `ExportToPdfCommand` - Export modlist to PDF

**Callbacks:**
- `OnGetInstalledMods` - Get current mod list
- `OnLoadPreset` - Load preset into MainWindow
- `OnReportStatus` - Report status message

**Dependencies:** ModListService, DialogService, UserConfigurationService

#### CloudModlistViewModel
**Purpose:** Handles Firebase cloud storage operations for modlists

**Key Properties:**
- `CloudModlists` (ObservableCollection) - Available cloud modlists
- `SelectedCloudModlist` (CloudModlistListEntry) - Selected modlist
- `IsCloudRefreshInProgress` (bool)

**Key Commands:**
- `SaveToCloudCommand` - Upload modlist to cloud
- `LoadFromCloudCommand` - Download and load modlist
- `DeleteCloudModlistCommand` - Delete cloud modlist
- `RefreshCloudModlistsCommand` - Refresh cloud list
- `ManageCloudModlistsCommand` - Open management dialog

**Callbacks:**
- `OnSaveToCloudAsync` - Save modlist to cloud
- `OnLoadCloudModlistAsync` - Load cloud modlist
- `OnRequestCloudRefresh` - Refresh cloud modlist list
- `OnReportStatus` - Report status message

**Dependencies:** FirebaseModlistService, UserConfigurationService

**CanExecute Logic:** Commands disabled when internet access is disabled

#### ModSelectionViewModel
**Purpose:** Custom multi-select tracking with Ctrl/Shift/Range support

**Key Properties:**
- `SelectedMods` (IReadOnlyList) - Currently selected mods
- `SelectionAnchor` (ModListItemViewModel) - Anchor for range selection
- `HasSelection` (bool) - Has any selection
- `HasMultipleSelection` (bool) - Multiple mods selected
- `SingleSelection` (ModListItemViewModel) - Single selected mod

**Key Methods:**
- `HandleModRowSelection()` - Process Ctrl/Shift/Range selection
- `AddToSelection()` - Add mod to selection
- `RemoveFromSelection()` - Remove mod from selection
- `ClearSelection()` - Clear all selections
- `GetSelectionSnapshot()` - Get selection for preservation
- `RestoreSelectionFromSourcePaths()` - Restore selection after refresh

**Key Commands:**
- `SelectAllModsCommand` - Select all visible mods
- `ClearSelectionCommand` - Clear selection

**Callbacks:**
- `OnSelectionChanged` - Selection changed (update UI buttons)
- `OnGetModsInViewOrder` - Get mods in current view order
- `OnFindModBySourcePath` - Find mod by source path
- `OnSelectedModPropertyChanged` - Selected mod property changed

**Algorithm:** Sophisticated multi-select with range calculation and preservation

#### ModListUIStateViewModel
**Purpose:** Centralized UI state flag management

**Key Properties:**
- `IsApplyingPreset` (bool)
- `IsModUpdateInProgress` (bool)
- `IsAutomaticRefreshRunning` (bool)
- `IsCloudModlistRefreshInProgress` (bool)
- `IsApplyingMultiToggle` (bool)
- `IsModUsageDialogOpen` (bool)
- ... 14 more boolean flags

**Computed Properties:**
- `IsAnyOperationInProgress` - Any blocking operation in progress
- `CanInteract` - UI is safe to interact with

**Purpose:** Replaces 20+ scattered boolean fields with centralized observable state

### Service Layer

#### ModListService
**Purpose:** Preset/modlist serialization, validation, file I/O

**Key Methods:**
```csharp
Task<ModListPreset> BuildPresetAsync(IReadOnlyList<ModEntry> mods, Dictionary<string, object>? configs)
Task<ModListPreset> LoadPresetAsync(string filePath, CancellationToken ct)
Task SavePresetAsync(ModListPreset preset, string filePath, CancellationToken ct)
Task<byte[]> GeneratePdfAsync(ModListPreset preset, string createdBy)
```

**Dependencies:** None (pure business logic)

#### FirebaseModlistService
**Purpose:** Cloud operations wrapper for Firebase

**Key Methods:**
```csharp
Task InitializeAsync(CancellationToken ct)
Task<List<CloudModlistListEntry>> GetModlistsAsync(string userId, CancellationToken ct)
Task SaveModlistAsync(CloudModlist modlist, CancellationToken ct)
Task DeleteModlistAsync(string modlistId, CancellationToken ct)
```

**Dependencies:** FirebaseModlistStore

#### ModInstallService
**Purpose:** Mod installation orchestration

**Key Methods:**
```csharp
Task<bool> InstallModAsync(ModEntry mod, string targetDirectory, CancellationToken ct)
Task<bool> TryFixModAsync(ModEntry mod, CancellationToken ct)
Task<byte[]> DownloadModAsync(string modId, string version, CancellationToken ct)
```

**Dependencies:** ModDatabaseService, FileSystem operations

#### ModSelectionService
**Purpose:** Selection algorithm logic (pure functions)

**Key Methods:**
```csharp
List<T> CalculateNewSelection<T>(T clickedItem, bool isCtrl, bool isShift, List<T> currentSelection, T? anchor)
List<T> CalculateRangeSelection<T>(List<T> items, T start, T end, bool preserveExisting)
```

**Dependencies:** None (pure algorithm)

#### DialogService
**Purpose:** Centralized dialog management

**Key Methods:**
```csharp
Task<(bool success, DialogResult result)> ShowSaveModlistDialogAsync(string? suggestedName)
Task<bool> ShowConfirmationAsync(string message, string title)
Task<CloudModlistListEntry?> ShowCloudSlotSelectionDialogAsync(List<CloudModlistListEntry> modlists)
```

**Dependencies:** WPF Dialogs (abstracted for testing)

## Data Flow Examples

### Example 1: Install Mod from Database

```
User clicks Install Button
    │
    ▼
MainWindow.InstallModButton_OnClick()
    │
    ▼
ModOperationsViewModel.InstallModCommand.Execute()
    │
    ├─> OnRequestAutomaticBackupAsync()  (callback to MainWindow)
    │       │
    │       ▼
    │   DataBackupViewModel.TryEnsureDataBackupBeforeLaunchAsync()
    │       │
    │       ▼
    │   DataBackupService.CreateBackupAsync()
    │
    ├─> ModInstallService.InstallModAsync()
    │       │
    │       ├─> ModDatabaseService.GetModAsync()
    │       ├─> Download mod file
    │       └─> Extract to Mods folder
    │
    └─> OnRequestRefreshAsync()  (callback to MainWindow)
            │
            ▼
        MainWindow.RefreshModsAsync()
            │
            ▼
        MainViewModel.LoadModsAsync()
```

### Example 2: Save Modlist to Cloud

```
User clicks Save to Cloud
    │
    ▼
MainWindow.SaveModlistToCloudButton_OnClick()
    │
    ▼
CloudModlistViewModel.SaveToCloudCommand.Execute()
    │
    ├─> OnSaveToCloudAsync()  (callback to MainWindow)
    │       │
    │       ▼
    │   PresetManagementViewModel.SaveModlistInternal()
    │       │
    │       ├─> OnGetInstalledMods()  (callback to MainWindow)
    │       │       │
    │       │       ▼
    │       │   MainWindow returns current mod list
    │       │
    │       └─> ModListService.BuildPresetAsync()
    │               │
    │               └─> Creates ModListPreset object
    │
    ├─> FirebaseModlistService.SaveModlistAsync()
    │       │
    │       └─> Upload to Firebase
    │
    └─> OnRequestCloudRefresh()  (callback to MainWindow)
            │
            ▼
        CloudModlistViewModel.RefreshCloudModlistsCommand.Execute()
            │
            ▼
        FirebaseModlistService.GetModlistsAsync()
```

### Example 3: Ctrl+Click Selection

```
User Ctrl+clicks mod row
    │
    ▼
MainWindow.ModsDataGrid_OnPreviewMouseLeftButtonDown()
    │
    ├─> Detect Ctrl key pressed
    │
    └─> ModSelectionViewModel.HandleModRowSelection(mod, isShift=false, isCtrl=true)
            │
            ├─> Check if mod already selected
            │
            ├─> If selected: RemoveFromSelection(mod)
            │   Else: AddToSelection(mod)
            │       │
            │       ├─> Update mod.IsSelected = true
            │       ├─> SubscribeToMod() for property changes
            │       └─> Add to _selectedMods collection
            │
            ├─> Update SelectionAnchor = mod
            │
            └─> Raise OnSelectionChanged event
                    │
                    ▼
                MainWindow.UpdateSelectedModButtons()
                    │
                    └─> Update button states based on selection
```

## Communication Patterns

### 1. View → ViewModel (Commands)

Event handlers call ViewModel commands:

```csharp
// In MainWindow
private async void SavePresetMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    _presetManagementViewModel?.SavePresetCommand.Execute(null);
}
```

### 2. ViewModel → View (Callbacks)

ViewModels use callbacks to request View operations:

```csharp
// In ViewModel
public event Func<Task>? OnRequestRefreshAsync;

// In MainWindow initialization
_modOperationsViewModel.OnRequestRefreshAsync += async () => await RefreshModsAsync();

// In ViewModel
await OnRequestRefreshAsync?.Invoke();
```

### 3. ViewModel → Service (Direct Calls)

ViewModels call Services directly:

```csharp
// In ViewModel
private readonly ModListService _modListService;

public async Task SaveModlistAsync()
{
    var preset = await _modListService.BuildPresetAsync(mods, configs);
    await _modListService.SavePresetAsync(preset, filePath, ct);
}
```

### 4. ViewModel → ViewModel (Shared State)

ViewModels communicate through MainViewModel or callbacks:

```csharp
// ModOperationsViewModel triggers refresh
await OnRequestRefreshAsync?.Invoke();

// MainWindow refreshes MainViewModel
await _viewModel.LoadModsAsync();

// MainViewModel updates ModSelectionViewModel
_modSelectionViewModel?.RestoreSelectionFromSourcePaths(paths, anchor);
```

## MVVM Toolkit Patterns

### [ObservableProperty]

Replaces manual SetProperty calls:

```csharp
// Before
private bool _isBusy;
public bool IsBusy
{
    get => _isBusy;
    set => SetProperty(ref _isBusy, value);
}

// After
[ObservableProperty]
private bool _isBusy;

// Generates:
// public bool IsBusy { get; set; }
// with proper INotifyPropertyChanged
```

### [RelayCommand]

Replaces manual command creation:

```csharp
// Before
private readonly RelayCommand _saveCommand;
public IRelayCommand SaveCommand { get; }

// In constructor:
_saveCommand = new RelayCommand(Save, CanSave);
SaveCommand = _saveCommand;

// After
[RelayCommand(CanExecute = nameof(CanSave))]
private void Save()
{
    // Implementation
}

private bool CanSave() => !IsBusy;

// Generates:
// public IRelayCommand SaveCommand { get; }
```

### Partial void OnChanged

Handle property change side effects:

```csharp
[ObservableProperty]
private int _totalMods;

partial void OnTotalModsChanged(int value)
{
    OnPropertyChanged(nameof(SummaryText));
}

public string SummaryText => $"{TotalMods} mods";
```

## Testing Strategy

### Unit Tests

**ViewModels:**
- Test commands execute correctly
- Test property changes raise notifications
- Test CanExecute logic
- Mock callbacks for isolation

**Services:**
- Test business logic in isolation
- Test error handling
- Test cancellation
- Mock dependencies

### Integration Tests

- Test ViewModel → Service interaction
- Test callback chains
- Test state management across components

### Manual Tests

- Full regression testing (100+ scenarios)
- Performance testing
- UI responsiveness
- Error handling

## Performance Considerations

### ObservableCollection Batching

MainViewModel uses `BatchedObservableCollection` for bulk operations:

```csharp
_mods.BeginBulkOperation();
try
{
    foreach (var mod in newMods)
        _mods.Add(mod);
}
finally
{
    _mods.EndBulkOperation(); // Raises single CollectionChanged event
}
```

### Debounced Search

Search is debounced to avoid excessive filtering:

```csharp
partial void OnSearchTextChanged(string value)
{
    // Debounce: 100-300ms delay before triggering search
    TriggerDebouncedInstalledModsSearch();
}
```

### Background Operations

Long-running operations use Task.Run:

```csharp
await Task.Run(async () =>
{
    // Heavy computation
}, cancellationToken);
```

### UI Thread Marshalling

Dispatcher used for UI updates from background:

```csharp
await InvokeOnDispatcherAsync(() =>
{
    mod.UpdateProperty();
}, cancellationToken, DispatcherPriority.Background);
```

## Best Practices

### 1. Always Use MVVM Toolkit Patterns

✅ **Good:**
```csharp
[ObservableProperty]
private bool _isLoading;

[RelayCommand]
private async Task LoadAsync() { }
```

❌ **Bad:**
```csharp
private bool _isLoading;
public bool IsLoading
{
    get => _isLoading;
    set => SetProperty(ref _isLoading, value);
}

public ICommand LoadCommand { get; }
```

### 2. Keep ViewModels UI-Agnostic

✅ **Good:**
```csharp
public event Func<bool>? OnRequestConfirmation;

if (OnRequestConfirmation?.Invoke() == false) return;
```

❌ **Bad:**
```csharp
var result = MessageBox.Show("Confirm?", "Title", MessageBoxButton.YesNo);
if (result != MessageBoxResult.Yes) return;
```

### 3. Use Callbacks for View Operations

✅ **Good:**
```csharp
public event Action? OnOperationComplete;
OnOperationComplete?.Invoke();
```

❌ **Bad:**
```csharp
((MainWindow)Application.Current.MainWindow).RefreshMods();
```

### 4. Validate in CanExecute

✅ **Good:**
```csharp
[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { }

private bool CanSave() => !string.IsNullOrWhiteSpace(Name);
```

❌ **Bad:**
```csharp
[RelayCommand]
private void Save()
{
    if (string.IsNullOrWhiteSpace(Name)) return;
}
```

### 5. Handle Cancellation

✅ **Good:**
```csharp
[RelayCommand]
private async Task LoadAsync(CancellationToken ct)
{
    await _service.LoadAsync(ct);
}
```

❌ **Bad:**
```csharp
[RelayCommand]
private async Task LoadAsync()
{
    await _service.LoadAsync(CancellationToken.None);
}
```

## Future Architectural Improvements

### 1. Dependency Injection

Current: Manual instantiation
Future: Use DI container

```csharp
// Future
public MainWindow(
    MainViewModel mainViewModel,
    DataBackupViewModel backupViewModel,
    ModOperationsViewModel operationsViewModel)
{
    _viewModel = mainViewModel;
    _backupViewModel = backupViewModel;
    _operationsViewModel = operationsViewModel;
}
```

### 2. Event Aggregator

Replace callbacks with event aggregator:

```csharp
// Future
_eventAggregator.Publish(new ModsRefreshRequested());
_eventAggregator.Subscribe<ModsRefreshRequested>(OnModsRefreshRequested);
```

### 3. XAML Command Bindings

Replace event handlers with bindings:

```xaml
<!-- Current -->
<Button Click="SavePresetMenuItem_OnClick" />

<!-- Future -->
<Button Command="{Binding Presets.SavePresetCommand}" />
```

### 4. Unit Tests

Add comprehensive test coverage:

```
VintageStoryModManager.Tests/
├── ViewModels/
│   ├── ModSelectionViewModelTests.cs
│   ├── PresetManagementViewModelTests.cs
│   └── ...
└── Services/
    ├── ModListServiceTests.cs
    ├── FirebaseModlistServiceTests.cs
    └── ...
```

## Conclusion

The current architecture successfully separates concerns into:
- **View:** UI-specific operations only
- **ViewModel:** Presentation logic, observable state
- **Service:** Business logic, reusable components
- **Model:** Data structures

All components follow MVVM Toolkit patterns and are designed for testability.

---

**Document Version:** 1.0
**Last Updated:** 2026-01-24
**Architecture Status:** ✅ Complete
