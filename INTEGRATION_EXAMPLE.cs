// This file demonstrates how Phase 1 services will integrate in Phase 2-3 ViewModels
// DO NOT COMPILE - This is for illustration purposes only

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// Example: How PresetManagementViewModel will use ModListService and DialogService in Phase 3
/// </summary>
public partial class PresetManagementViewModel : ObservableObject
{
    private readonly ModListService _modListService;
    private readonly DialogService _dialogService;
    private readonly UserConfigurationService _userConfiguration;
    private readonly Window _owner;

    // MVVM Toolkit properties (Phase 5 modernization pattern)
    [ObservableProperty]
    private bool _isSavingModlist;

    [ObservableProperty]
    private double _saveProgress;

    [ObservableProperty]
    private string _saveStatusMessage = string.Empty;

    public PresetManagementViewModel(
        ModListService modListService,
        DialogService dialogService,
        UserConfigurationService userConfiguration,
        Window owner)
    {
        _modListService = modListService;
        _dialogService = dialogService;
        _userConfiguration = userConfiguration;
        _owner = owner;
    }

    /// <summary>
    /// Command to save modlist - uses ModListService and DialogService
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveModlist))]
    private async Task SaveModlistAsync()
    {
        // 1. Show dialog (DialogService)
        var configOptions = BuildModConfigOptions();
        var uploaderName = GetUploaderName();

        var dialogResult = _dialogService.ShowSaveModlistDialog(
            owner: _owner,
            suggestedName: null,
            configOptions: configOptions,
            uploaderName: uploaderName,
            defaultGameVersion: GetInstalledGameVersion());

        if (dialogResult is null)
            return; // User cancelled

        IsSavingModlist = true;
        SaveProgress = 0;

        try
        {
            var modStates = GetCurrentModStates();
            var includedConfigs = ReadModConfigurations(dialogResult.SelectedConfigOptions);

            // 2. Handle PDF export
            if (dialogResult.SelectedAction == SaveInstalledModsDialogResult.SavePdf)
            {
                SaveStatusMessage = "Generating PDF...";
                SaveProgress = 50;

                var success = _modListService.GeneratePdf(
                    filePath: GetPdfFilePath(dialogResult.ListName),
                    listName: dialogResult.ListName,
                    modlistVersion: dialogResult.Version,
                    description: dialogResult.Description,
                    uploaderName: dialogResult.CreatedBy ?? uploaderName,
                    gameVersion: dialogResult.VintageStoryVersion,
                    mods: GetInstalledMods(),
                    includedConfigurations: includedConfigs,
                    modStates: modStates);

                if (success)
                {
                    SaveProgress = 100;
                    SaveStatusMessage = $"Saved PDF: {dialogResult.ListName}";
                }
                else
                {
                    _dialogService.ShowError(_owner, "Failed to generate PDF.");
                }
            }
            // 3. Handle JSON export (ModListService)
            else
            {
                SaveStatusMessage = "Building modlist JSON...";
                SaveProgress = 30;

                if (!_modListService.TryBuildModlistJson(
                    modlistName: dialogResult.ListName,
                    modStates: modStates,
                    description: dialogResult.Description,
                    version: dialogResult.Version,
                    uploader: dialogResult.CreatedBy ?? uploaderName,
                    includedConfigurations: includedConfigs,
                    gameVersion: dialogResult.VintageStoryVersion,
                    out var json))
                {
                    _dialogService.ShowError(_owner, "Failed to build modlist JSON.");
                    return;
                }

                SaveStatusMessage = "Saving modlist file...";
                SaveProgress = 60;

                var filePath = GetModlistFilePath(dialogResult.ListName);
                var preset = BuildPresetFromJson(json);

                if (_modListService.SavePresetToFile(filePath, preset))
                {
                    SaveProgress = 100;
                    SaveStatusMessage = $"Saved modlist: {dialogResult.ListName}";

                    // Trigger UI refresh (callback pattern from ModBrowserViewModel)
                    OnModlistSaved?.Invoke(filePath);
                }
                else
                {
                    _dialogService.ShowError(_owner, "Failed to save modlist file.");
                }
            }
        }
        finally
        {
            IsSavingModlist = false;
        }
    }

    private bool CanSaveModlist() => !IsSavingModlist && HasInstalledMods();

    // Callback for notifying other components
    public event Action<string>? OnModlistSaved;

    // Helper methods would go here...
    private List<ModConfigOption> BuildModConfigOptions() => throw new NotImplementedException();
    private string GetUploaderName() => throw new NotImplementedException();
    private string GetInstalledGameVersion() => throw new NotImplementedException();
    private List<ModPresetModState> GetCurrentModStates() => throw new NotImplementedException();
    private Dictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? ReadModConfigurations(IReadOnlyList<ModConfigOption> options) => throw new NotImplementedException();
    private string GetPdfFilePath(string name) => throw new NotImplementedException();
    private List<ModListItemViewModel> GetInstalledMods() => throw new NotImplementedException();
    private string GetModlistFilePath(string name) => throw new NotImplementedException();
    private SerializablePreset BuildPresetFromJson(string json) => throw new NotImplementedException();
    private bool HasInstalledMods() => throw new NotImplementedException();
}

/// <summary>
/// Example: How ModSelectionViewModel will use ModSelectionService in Phase 4
/// </summary>
public partial class ModSelectionViewModel : ObservableObject
{
    private readonly ModSelectionService _selectionService;

    [ObservableProperty]
    private List<ModListItemViewModel> _selectedMods = new();

    [ObservableProperty]
    private int? _selectionAnchorIndex;

    public bool HasSelectedMods => SelectedMods.Count > 0;
    public bool HasMultipleSelectedMods => SelectedMods.Count > 1;

    public ModSelectionViewModel(ModSelectionService selectionService)
    {
        _selectionService = selectionService;
    }

    /// <summary>
    /// Command to handle mod selection (called from UI event handler)
    /// </summary>
    [RelayCommand]
    private void SelectMod(ModSelectionEventArgs args)
    {
        var allMods = GetModsInViewOrder();
        var clickedIndex = allMods.IndexOf(args.ClickedMod);

        if (clickedIndex < 0) return;

        // Get current selection as indices
        var currentIndices = _selectionService.FindIndices(allMods, SelectedMods);

        // Calculate new selection using ModSelectionService
        var result = _selectionService.CalculateNewSelection(
            clickedIndex: clickedIndex,
            currentSelection: currentIndices,
            anchorIndex: SelectionAnchorIndex,
            isShiftPressed: args.IsShiftPressed,
            isCtrlPressed: args.IsCtrlPressed,
            viewSize: allMods.Count);

        // Resolve indices back to mods
        var newSelectedMods = _selectionService.ResolveSelection(allMods, result.SelectedIndices);

        // Update UI
        UpdateSelectedMods(newSelectedMods.ToList());
        SelectionAnchorIndex = result.NewAnchorIndex;

        // Notify other components
        OnSelectionChanged?.Invoke(SelectedMods);
    }

    [RelayCommand]
    private void SelectAllMods()
    {
        var allMods = GetModsInViewOrder();
        var allIndices = _selectionService.CalculateSelectAll(allMods.Count);
        var allSelected = _selectionService.ResolveSelection(allMods, allIndices);

        UpdateSelectedMods(allSelected.ToList());
        SelectionAnchorIndex = allMods.Count > 0 ? allMods.Count - 1 : null;

        OnSelectionChanged?.Invoke(SelectedMods);
    }

    [RelayCommand]
    private void ClearSelection()
    {
        UpdateSelectedMods(new List<ModListItemViewModel>());
        SelectionAnchorIndex = null;
        OnSelectionChanged?.Invoke(SelectedMods);
    }

    private void UpdateSelectedMods(List<ModListItemViewModel> newSelection)
    {
        // Unsubscribe from old selection
        foreach (var mod in SelectedMods)
            mod.IsSelected = false;

        // Subscribe to new selection
        SelectedMods = newSelection;
        foreach (var mod in SelectedMods)
            mod.IsSelected = true;

        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(HasMultipleSelectedMods));
    }

    // Callback for notifying other components (follows ModBrowserViewModel pattern)
    public event Action<List<ModListItemViewModel>>? OnSelectionChanged;

    private List<ModListItemViewModel> GetModsInViewOrder() => throw new NotImplementedException();
}

/// <summary>
/// Example: How CloudModlistViewModel will use FirebaseModlistService in Phase 3
/// </summary>
public partial class CloudModlistViewModel : ObservableObject
{
    private readonly FirebaseModlistService _firebaseService;
    private readonly DialogService _dialogService;

    [ObservableProperty]
    private bool _isCloudRefreshInProgress;

    [ObservableProperty]
    private List<CloudModlistListEntry> _cloudModlists = new();

    public CloudModlistViewModel(
        FirebaseModlistService firebaseService,
        DialogService dialogService)
    {
        _firebaseService = firebaseService;
        _dialogService = dialogService;
    }

    [RelayCommand(CanExecute = nameof(CanSaveToCloud))]
    private async Task SaveToCloudAsync()
    {
        try
        {
            // 1. Initialize Firebase (uses FirebaseModlistService)
            var playerUid = GetPlayerUid();
            var playerName = GetPlayerName();

            await _firebaseService.InitializeAsync(playerUid, playerName);

            // 2. Show cloud details dialog
            var dialogResult = _dialogService.ShowCloudModlistDetailsDialog(
                owner: GetOwner(),
                suggestedName: $"Modlist {DateTime.Now:yyyy-MM-dd HH:mm}",
                configOptions: BuildConfigOptions(),
                defaultGameVersion: GetInstalledGameVersion());

            if (dialogResult is null) return;

            // 3. Build modlist JSON
            var modlistJson = BuildModlistJson(dialogResult);

            // 4. Find available slot
            var slots = await GetAvailableSlotsAsync();
            var targetSlot = FindOrPromptForSlot(slots, dialogResult.ModlistName);

            if (targetSlot is null) return;

            // 5. Save to cloud (uses FirebaseModlistService)
            await _firebaseService.SaveModlistAsync(targetSlot, modlistJson);

            // 6. Refresh cloud modlists
            await RefreshCloudModlistsAsync();

            _dialogService.ShowInformation(
                GetOwner(),
                $"Saved \"{dialogResult.ModlistName}\" to the cloud.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                GetOwner(),
                $"Failed to save to cloud: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshCloudModlistsAsync()
    {
        IsCloudRefreshInProgress = true;

        try
        {
            // Uses FirebaseModlistService to get public registry
            var entries = await _firebaseService.GetRegistryEntriesAsync();

            CloudModlists = entries
                .Select(e => ConvertToListEntry(e))
                .ToList();
        }
        finally
        {
            IsCloudRefreshInProgress = false;
        }
    }

    private bool CanSaveToCloud() => !IsCloudRefreshInProgress;

    // Helper methods...
    private string GetPlayerUid() => throw new NotImplementedException();
    private string GetPlayerName() => throw new NotImplementedException();
    private Window GetOwner() => throw new NotImplementedException();
    private List<ModConfigOption> BuildConfigOptions() => throw new NotImplementedException();
    private string GetInstalledGameVersion() => throw new NotImplementedException();
    private string BuildModlistJson(CloudModlistDetailsDialogResult result) => throw new NotImplementedException();
    private Task<List<CloudModlistSlot>> GetAvailableSlotsAsync() => throw new NotImplementedException();
    private string? FindOrPromptForSlot(List<CloudModlistSlot> slots, string name) => throw new NotImplementedException();
    private CloudModlistListEntry ConvertToListEntry(CloudModlistRegistryEntry entry) => throw new NotImplementedException();
}

// Supporting classes for examples
public record ModSelectionEventArgs(
    ModListItemViewModel ClickedMod,
    bool IsShiftPressed,
    bool IsCtrlPressed);
