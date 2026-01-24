using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for preset and modlist management (save/load operations).
/// Extracted from MainWindow.xaml.cs Phase 3.
/// </summary>
public partial class PresetManagementViewModel : ObservableObject
{
    private readonly ModListService _modListService;
    private readonly UserConfigurationService _userConfiguration;
    private readonly Window _owner;
    private readonly Func<string?> _getInstalledGameVersion;
    private readonly Func<IReadOnlyList<ModListItemViewModel>> _getInstalledMods;
    private readonly Func<IReadOnlyList<ModPresetModState>> _getModStates;

    [ObservableProperty]
    private bool _isSavingModlist;

    [ObservableProperty]
    private double _saveProgress;

    [ObservableProperty]
    private string _saveStatusMessage = string.Empty;

    public PresetManagementViewModel(
        ModListService modListService,
        UserConfigurationService userConfiguration,
        Window owner,
        Func<string?> getInstalledGameVersion,
        Func<IReadOnlyList<ModListItemViewModel>> getInstalledMods,
        Func<IReadOnlyList<ModPresetModState>> getModStates)
    {
        _modListService = modListService;
        _userConfiguration = userConfiguration;
        _owner = owner;
        _getInstalledGameVersion = getInstalledGameVersion;
        _getInstalledMods = getInstalledMods;
        _getModStates = getModStates;
    }

    /// <summary>
    /// Saves the current mod setup as a preset (simple JSON, no metadata).
    /// </summary>
    [RelayCommand]
    private void SavePreset()
    {
        OnRequestSavePreset?.Invoke();
    }

    /// <summary>
    /// Saves the current mod setup as a modlist (JSON with metadata: name, version, description, configs).
    /// </summary>
    [RelayCommand]
    private void SaveModlist()
    {
        SaveModlistInternal(null);
    }

    /// <summary>
    /// Saves the current mod setup as a modlist with a suggested name provider.
    /// Returns true if saved successfully.
    /// </summary>
    public bool SaveModlistInternal(Func<string?>? suggestedNameProvider)
    {
        var mods = _getInstalledMods();
        if (mods.Count == 0)
        {
            ModManagerMessageBox.Show(
                "No installed mods were found to include in the modlist.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var configOptions = OnBuildModConfigOptions?.Invoke() ?? new List<ModConfigOption>();
        var suggestedName = suggestedNameProvider?.Invoke();
        var uploaderName = OnGetUploaderName?.Invoke() ?? string.Empty;

        var metadataDialog = new SaveInstalledModsDialog(
            suggestedName,
            configOptions,
            uploaderName,
            defaultVersion: null,
            defaultGameVersion: _getInstalledGameVersion(),
            SaveInstalledModsDialogResult.SaveJson)
        {
            Owner = _owner
        };

        var dialogResult = metadataDialog.ShowDialog();
        if (dialogResult != true) return false;

        var listName = metadataDialog.ListName;
        var version = metadataDialog.Version;
        var description = metadataDialog.Description;
        var createdBy = string.IsNullOrWhiteSpace(metadataDialog.CreatedBy)
            ? uploaderName
            : metadataDialog.CreatedBy!.Trim();
        var gameVersion = OnResolveGameVersion?.Invoke(metadataDialog.VintageStoryVersion) ?? metadataDialog.VintageStoryVersion;

        var selectedConfigOptions = metadataDialog.GetSelectedConfigOptions();
        var includedConfigurations = OnReadModConfigurations?.Invoke(selectedConfigOptions);

        // Handle PDF export
        if (metadataDialog.SelectedAction == SaveInstalledModsDialogResult.SavePdf)
        {
            return SaveModlistAsPdf(listName, version, description, createdBy, gameVersion, includedConfigurations);
        }

        // Handle JSON export
        return SaveModlistAsJson(listName, version, description, createdBy, gameVersion, includedConfigurations, suggestedName);
    }

    private bool SaveModlistAsJson(
        string listName,
        string? version,
        string? description,
        string createdBy,
        string? gameVersion,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
        string? suggestedName)
    {
        try
        {
            var modListDirectory = OnEnsureModListDirectory?.Invoke();
            if (string.IsNullOrWhiteSpace(modListDirectory))
            {
                ModManagerMessageBox.Show(
                    "The Modlists directory could not be accessed.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var suggestedEntryName = !string.IsNullOrWhiteSpace(listName) ? listName : suggestedName;
            var entryName = OnBuildSuggestedFileName?.Invoke(suggestedEntryName, "Modlist") ?? listName;
            var filePath = Path.Combine(modListDirectory!, entryName + ".json");

            if (File.Exists(filePath))
            {
                var message =
                    $"A modlist named \"{Path.GetFileName(filePath)}\" already exists in the Modlists folder. Do you want to replace it?";
                var confirmation = ModManagerMessageBox.Show(
                    _owner,
                    message,
                    "Replace Modlist",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes) return false;
            }

            var modStates = _getModStates();
            var preset = _modListService.BuildSerializablePreset(
                entryName,
                modStates,
                true,
                true,
                includedConfigurations,
                gameVersion);

            if (!string.IsNullOrWhiteSpace(listName)) preset.Name = listName.Trim();
            preset.Description = description;
            preset.Version = version;
            preset.Uploader = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim();

            if (!_modListService.SavePresetToFile(filePath, preset))
            {
                ModManagerMessageBox.Show(
                    "Failed to save the modlist.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            OnReportStatus?.Invoke($"Saved modlist \"{entryName}\".", false);
            OnModlistSaved?.Invoke(filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                      or PathTooLongException)
        {
            ModManagerMessageBox.Show($"Failed to save the modlist:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveModlistAsPdf(
        string listName,
        string? version,
        string? description,
        string createdBy,
        string? gameVersion,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations)
    {
        var mods = _getInstalledMods();
        var modStates = _getModStates();

        try
        {
            var modListDirectory = OnEnsureModListDirectory?.Invoke();
            if (string.IsNullOrWhiteSpace(modListDirectory))
            {
                ModManagerMessageBox.Show(
                    "The Modlists directory could not be accessed.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var entryName = OnBuildSuggestedFileName?.Invoke(listName, "Modlist") ?? listName;
            var filePath = Path.Combine(modListDirectory!, entryName + ".pdf");

            if (File.Exists(filePath))
            {
                var message =
                    $"A modlist PDF named \"{Path.GetFileName(filePath)}\" already exists in the Modlists folder. Do you want to replace it?";
                var confirmation = ModManagerMessageBox.Show(
                    _owner,
                    message,
                    "Replace Modlist PDF",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes) return false;
            }

            var success = _modListService.GeneratePdf(
                filePath,
                listName,
                version,
                description,
                createdBy,
                gameVersion,
                mods,
                includedConfigurations,
                modStates);

            if (success)
            {
                OnReportStatus?.Invoke($"Saved PDF for {listName}.", false);
                return true;
            }
            else
            {
                ModManagerMessageBox.Show(
                    "Failed to generate PDF.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                      or PathTooLongException or SecurityException)
        {
            ModManagerMessageBox.Show($"Failed to prepare the Modlists folder:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Loads a preset or modlist from a file.
    /// </summary>
    [RelayCommand]
    private async Task LoadPresetAsync()
    {
        await (OnRequestLoadPreset?.Invoke() ?? Task.CompletedTask);
    }

    /// <summary>
    /// Saves an automatic modlist (for internal use, no metadata dialog).
    /// </summary>
    public bool TrySaveAutomaticModlist(string requestedName, out string savedName, out string filePath)
    {
        savedName = string.Empty;
        filePath = string.Empty;

        var modStates = _getModStates();
        if (modStates.Count == 0) return false;

        var modListDirectory = OnEnsureRebuiltModListDirectory?.Invoke();
        if (string.IsNullOrWhiteSpace(modListDirectory)) return false;

        savedName = OnBuildSuggestedFileName?.Invoke(requestedName, "Modlist") ?? requestedName;
        filePath = Path.Combine(modListDirectory!, savedName + ".json");

        var gameVersion = OnResolveGameVersion?.Invoke(null);
        var preset = _modListService.BuildSerializablePreset(savedName, modStates, true, true, null, gameVersion);

        if (!_modListService.SavePresetToFile(filePath, preset))
        {
            savedName = string.Empty;
            filePath = string.Empty;
            return false;
        }

        OnReportStatus?.Invoke($"Saved modlist \"{savedName}\".", false);
        return true;
    }

    /// <summary>
    /// Builds modlist JSON for cloud upload.
    /// </summary>
    public bool TryBuildCurrentModlistJson(
        string modlistName,
        string? description,
        string? version,
        string uploader,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
        string? gameVersion,
        out string json)
    {
        var modStates = _getModStates();
        return _modListService.TryBuildModlistJson(
            modlistName,
            modStates,
            description,
            version,
            uploader,
            includedConfigurations,
            gameVersion,
            out json);
    }

    /// <summary>
    /// Callback for requesting preset save (handles file dialog and directory setup).
    /// MainWindow implements the complex TrySaveSnapshot logic.
    /// </summary>
    public event Action? OnRequestSavePreset;

    /// <summary>
    /// Callback for requesting preset load (handles file dialog and modlist loading).
    /// MainWindow implements the complex load logic with dialogs.
    /// </summary>
    public event Func<Task>? OnRequestLoadPreset;

    /// <summary>
    /// Callback for reporting status messages.
    /// </summary>
    public event Action<string, bool>? OnReportStatus;

    /// <summary>
    /// Callback for building mod config options.
    /// </summary>
    public event Func<List<ModConfigOption>>? OnBuildModConfigOptions;

    /// <summary>
    /// Callback for getting uploader name for PDF/modlist.
    /// </summary>
    public event Func<string>? OnGetUploaderName;

    /// <summary>
    /// Callback for resolving game version.
    /// </summary>
    public event Func<string?, string?>? OnResolveGameVersion;

    /// <summary>
    /// Callback for reading mod configurations from selected options.
    /// </summary>
    public event Func<IReadOnlyList<ModConfigOption>, IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>?>? OnReadModConfigurations;

    /// <summary>
    /// Callback for ensuring modlist directory exists.
    /// </summary>
    public event Func<string?>? OnEnsureModListDirectory;

    /// <summary>
    /// Callback for ensuring rebuilt modlist directory exists.
    /// </summary>
    public event Func<string?>? OnEnsureRebuiltModListDirectory;

    /// <summary>
    /// Callback for building suggested file name.
    /// </summary>
    public event Func<string?, string, string>? OnBuildSuggestedFileName;

    /// <summary>
    /// Event raised when a modlist is saved.
    /// Consumers should refresh local modlists.
    /// </summary>
    public event Action<string>? OnModlistSaved;
}
