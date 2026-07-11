# Best Practice Refactoring Opportunities

This document lists recommended improvements for the codebase, ranked by ease of implementation and code impact. For each category, relevant areas in the codebase are identified.

---

## 1. Use CommunityToolkit.Mvvm Attributes for Property Change Notification and Commands
**MainViewModel.cs Recommendations:**
- Refactor all private backing fields and public properties using `SetProperty` to `[ObservableProperty]` fields.
- Remove explicit `OnPropertyChanged` calls except for computed properties.
- Use `[NotifyPropertyChangedFor]` for dependent/computed properties (e.g., `HasStatusMessage`, `SummaryText`).

**Description:** Replace manual property change notifications and command wiring with `[ObservableProperty]`, `[NotifyPropertyChangedFor]`, and `[RelayCommand]` attributes.

**Relevant Areas & Examples:**
- ViewModels/MainViewModel.cs
	- Manual property change notification via `SetProperty(ref _field, value)` and `OnPropertyChanged(...)` (e.g., properties like `IsBusy`, `IsLoadingMods`, `SelectedSortOption`).
	- Recommendation: Use `[ObservableProperty]` for backing fields and auto-generate notifications.
	- Example:
		```csharp
		[ObservableProperty]
		private bool isBusy;
		```
	- Remove explicit `OnPropertyChanged` calls where possible.
- ViewModels/ModConfigurationViewModel.cs
- ViewModels/UpdateModSelectionViewModel.cs
- ViewModels/ServerTargetEditorViewModel.cs
- ViewModels/ModListItemViewModel.cs

---

## 2. Manual Command Wiring → [RelayCommand] Attributes
**MainViewModel.cs Recommendations:**
- Replace all manual `RelayCommand` and `AsyncRelayCommand` fields and assignments with `[RelayCommand]` or `[AsyncRelayCommand]` attributes on methods.
- Remove manual `NotifyCanExecuteChanged` calls; use toolkit's auto-generated command properties.

**Description:** Replace manual command instantiation with `[RelayCommand]` for cleaner, auto-generated commands.

**Relevant Areas & Examples:**
- ViewModels/MainViewModel.cs
	- Manual command fields and instantiation, e.g.:
		```csharp
		_clearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => HasSearchText);
		ClearSearchCommand = _clearSearchCommand;
		```
	- Recommendation: Use `[RelayCommand]` on methods, remove manual fields, and let toolkit generate command properties.
	- Example:
		```csharp
		[RelayCommand]
		private void ClearSearch() => SearchText = string.Empty;
		```
- ViewModels/ModConfigurationViewModel.cs
- ViewModels/UpdateModSelectionViewModel.cs

---

## 3. Move Business Logic Out of Code-Behind and Into ViewModels
**Description:** Refactor business logic and state management from code-behind files to ViewModels, using commands and data binding.

**Relevant Areas:**
- Views/ModBrowserView.xaml.cs
- Views/Dialogs/MessageDialogWindow.xaml.cs
- Views/Dialogs/ModUsageNoIssuesDialog.xaml.cs

---

## 4. Move File and Stream Access Out of ViewModels
**Description:** Move direct file I/O from ViewModels to dedicated service classes, injected via constructor.

**Relevant Areas:**
- ViewModels/ModConfigurationViewModel.cs

---

## 5. Refactor Static Helpers Out of ViewModels Into Services
**Description:** Move static helper methods from ViewModels to utility or service classes for better separation of concerns.

**Relevant Areas:**
- ViewModels/ModConfigurationViewModel.cs
- ViewModels/ModListItemViewModel.cs

---

## 6. Async Logic in Code-Behind → ViewModel Commands
**Description:** Move async logic from code-behind event handlers to ViewModel commands for better testability and MVVM compliance.

**Relevant Areas:**
- Views/ModBrowserView.xaml.cs

---

## 7. Use Dependency Injection for Services and Utilities
**MainViewModel.cs Recommendations:**
- Register all services (e.g., `ModDiscoveryService`, `ModDatabaseService`, `TagFilterService`, `ModDirectoryWatcher`, `ClientSettingsWatcher`) in the DI container.
- Refactor constructor to accept these services as parameters.
- Remove direct instantiation of services and use injected instances throughout the ViewModel.
**MainViewModel.cs Examples:**
- Move static utility methods (e.g., `BuildVoteEtagKey`, `IsDifferentVersion`, `CreateSearchTokens`, `GetDisplayPath`, `CreateSortOptions`) to dedicated utility/service classes.
- Inject these helpers if needed, or use as static utility classes.

**Description:** Refactor service instantiation to use a DI container, updating constructors and service usage throughout the app.

**Relevant Areas & Examples:**
- ViewModels/MainViewModel.cs
	- Direct instantiation of services in constructor, e.g.:
		```csharp
		_discoveryService = new ModDiscoveryService(_settingsStore);
		_databaseService = new ModDatabaseService();
		_tagFilterService = new TagFilterService(_tagCache);
		_modsWatcher = new ModDirectoryWatcher(_discoveryService);
		_clientSettingsWatcher = new ClientSettingsWatcher(_settingsStore.SettingsPath);
		```
	- Recommendation: Inject these services via constructor using a DI container (e.g., Microsoft.Extensions.DependencyInjection).
	- Example:
		```csharp
		public MainViewModel(ModDiscoveryService discoveryService, ModDatabaseService databaseService, ...)
		```
- ViewModels/ModConfigurationViewModel.cs
- Services/* (all service classes)

---

## 8. Refactor Static Utility Methods Into Injectable Services
**MainViewModel.cs Examples:**
- Any static logic in MainViewModel that is reused elsewhere (e.g., version comparison, path normalization) should be moved to injectable utility services for testability.
**Description:** Move static utility methods to injectable services for improved testability and extensibility.

**Relevant Areas:**
- Services/ModImageCacheService.cs
- Services/VersionStringUtility.cs
- Services/ModCompatibilityCommentsService.cs

---

*This list should be updated as refactoring progresses. Each item can be checked off as completed.*
