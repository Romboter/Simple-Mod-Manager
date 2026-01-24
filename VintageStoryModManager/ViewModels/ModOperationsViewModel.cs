using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for mod operations (install, update, delete, fix).
/// Extracted from MainWindow.xaml.cs Phase 2.
/// Follows ModBrowserViewModel callback pattern for MainWindow coordination.
/// </summary>
public partial class ModOperationsViewModel : ObservableObject
{
    private readonly ModUpdateService _modUpdateService;
    private readonly ModDatabaseService _modDatabaseService;
    private readonly ModActivityLoggingService _modActivityLoggingService;
    private readonly UserConfigurationService _userConfiguration;
    private readonly Window _owner;

    [ObservableProperty]
    private bool _isModUpdateInProgress;

    [ObservableProperty]
    private double _modUpdateProgress;

    [ObservableProperty]
    private string _modUpdateStatusMessage = string.Empty;

    public ModOperationsViewModel(
        ModUpdateService modUpdateService,
        ModDatabaseService modDatabaseService,
        ModActivityLoggingService modActivityLoggingService,
        UserConfigurationService userConfiguration,
        Window owner)
    {
        _modUpdateService = modUpdateService;
        _modDatabaseService = modDatabaseService;
        _modActivityLoggingService = modActivityLoggingService;
        _userConfiguration = userConfiguration;
        _owner = owner;
    }

    /// <summary>
    /// Deletes a single mod from disk.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteModOperation))]
    private async Task DeleteModAsync(ModListItemViewModel mod)
    {
        if (mod is null) return;

        var confirmation = ModManagerMessageBox.Show(
            $"Are you sure you want to delete {mod.DisplayName}? This will remove the mod from disk.",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        // Request automatic backup before deletion
        await (OnRequestAutomaticBackupAsync?.Invoke("ModsDeleted") ?? Task.CompletedTask);

        // Delegate deletion to caller (MainWindow has the path resolution logic)
        var deleted = OnDeleteMod != null ? OnDeleteMod.Invoke(mod) : false;

        if (deleted)
        {
            // Request mod list refresh
            await (OnRequestRefreshAsync?.Invoke() ?? Task.CompletedTask);
            OnReportStatus?.Invoke($"Deleted {mod.DisplayName}.", false);
        }
    }

    /// <summary>
    /// Deletes multiple mods from disk.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteModOperation))]
    private async Task DeleteMultipleModsAsync(IReadOnlyList<ModListItemViewModel> mods)
    {
        if (mods is null || mods.Count == 0) return;

        if (mods.Count == 1)
        {
            await DeleteModAsync(mods[0]);
            return;
        }

        var confirmation = ModManagerMessageBox.Show(
            $"Are you sure you want to delete {mods.Count} mods? This will remove them from disk.",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        // Request automatic backup before deletion
        await (OnRequestAutomaticBackupAsync?.Invoke("ModsDeleted") ?? Task.CompletedTask);

        var deletedCount = 0;
        foreach (var mod in mods)
        {
            var deleted = OnDeleteMod != null ? OnDeleteMod.Invoke(mod) : false;
            if (deleted) deletedCount++;
        }

        if (deletedCount > 0)
        {
            // Request mod list refresh
            await (OnRequestRefreshAsync?.Invoke() ?? Task.CompletedTask);
            OnReportStatus?.Invoke($"Deleted {deletedCount} mod(s).", false);
        }
    }

    /// <summary>
    /// Installs a mod from the mod database.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteModOperation))]
    private async Task InstallModAsync(ModListItemViewModel mod)
    {
        if (mod is null) return;

        if (!mod.HasDownloadableRelease)
        {
            ModManagerMessageBox.Show("No downloadable releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Request automatic backup before installation
        await (OnRequestAutomaticBackupAsync?.Invoke("ModsUpdated") ?? Task.CompletedTask);

        IsModUpdateInProgress = true;
        ModUpdateProgress = 0;
        ModUpdateStatusMessage = $"Installing {mod.DisplayName}...";

        try
        {
            // Delegate actual installation to caller (MainWindow has complex release selection logic)
            var installed = OnInstallModAsync != null ? await OnInstallModAsync.Invoke(mod) : false;

            if (installed)
            {
                ModUpdateProgress = 100;
                ModUpdateStatusMessage = $"Installed {mod.DisplayName} successfully.";
                OnReportStatus?.Invoke($"Installed {mod.DisplayName}.", false);

                // Request mod list refresh
                await (OnRequestRefreshAsync?.Invoke() ?? Task.CompletedTask);
            }
        }
        finally
        {
            IsModUpdateInProgress = false;
            ModUpdateProgress = 0;
            ModUpdateStatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Updates a single mod to the latest version.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteModOperation))]
    private async Task UpdateModAsync(ModListItemViewModel mod)
    {
        if (mod is null) return;

        // Request automatic backup before update
        await (OnRequestAutomaticBackupAsync?.Invoke("ModsUpdated") ?? Task.CompletedTask);

        IsModUpdateInProgress = true;
        ModUpdateProgress = 0;
        ModUpdateStatusMessage = $"Updating {mod.DisplayName}...";

        try
        {
            // Delegate actual update to caller (MainWindow has the UpdateModsAsync logic)
            var updated = OnUpdateModAsync != null ? await OnUpdateModAsync.Invoke(mod) : false;

            if (updated)
            {
                ModUpdateProgress = 100;
                ModUpdateStatusMessage = $"Updated {mod.DisplayName} successfully.";
                OnReportStatus?.Invoke($"Updated {mod.DisplayName}.", false);

                // Request mod list refresh
                await (OnRequestRefreshAsync?.Invoke() ?? Task.CompletedTask);
            }
        }
        finally
        {
            IsModUpdateInProgress = false;
            ModUpdateProgress = 0;
            ModUpdateStatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Updates all mods that have available updates.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteModOperation))]
    private async Task UpdateAllModsAsync()
    {
        // Request automatic backup before updates
        await (OnRequestAutomaticBackupAsync?.Invoke("ModsUpdated") ?? Task.CompletedTask);

        IsModUpdateInProgress = true;
        ModUpdateProgress = 0;
        ModUpdateStatusMessage = "Updating all mods...";

        try
        {
            // Delegate to caller (MainWindow has bulk update logic with dialogs)
            await (OnUpdateAllModsAsync?.Invoke() ?? Task.CompletedTask);
        }
        finally
        {
            IsModUpdateInProgress = false;
            ModUpdateProgress = 0;
            ModUpdateStatusMessage = string.Empty;
        }
    }

    /// <summary>
    /// Fixes missing or outdated dependencies for a mod.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteModOperation))]
    private async Task FixModDependenciesAsync(ModListItemViewModel mod)
    {
        if (mod is null) return;

        if (mod.Dependencies.Count == 0)
        {
            ModManagerMessageBox.Show("This mod does not declare dependencies that can be fixed automatically.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        IsModUpdateInProgress = true;
        ModUpdateProgress = 0;
        ModUpdateStatusMessage = $"Fixing dependencies for {mod.DisplayName}...";

        try
        {
            // Delegate dependency fixing to caller (complex logic in MainWindow)
            var wasFixed = OnFixModDependenciesAsync != null ? await OnFixModDependenciesAsync.Invoke(mod) : false;

            if (wasFixed)
            {
                ModUpdateProgress = 100;
                ModUpdateStatusMessage = $"Fixed dependencies for {mod.DisplayName}.";
                OnReportStatus?.Invoke($"Fixed dependencies for {mod.DisplayName}.", false);

                // Request mod list refresh
                await (OnRequestRefreshAsync?.Invoke() ?? Task.CompletedTask);
            }
        }
        finally
        {
            IsModUpdateInProgress = false;
            ModUpdateProgress = 0;
            ModUpdateStatusMessage = string.Empty;
        }
    }

    private bool CanExecuteModOperation() => !IsModUpdateInProgress;

    /// <summary>
    /// Callback for reporting status messages.
    /// </summary>
    public event Action<string, bool>? OnReportStatus;

    /// <summary>
    /// Callback for requesting automatic backup before mod operations.
    /// </summary>
    public event Func<string, Task>? OnRequestAutomaticBackupAsync;

    /// <summary>
    /// Callback for requesting mod list refresh.
    /// </summary>
    public event Func<Task>? OnRequestRefreshAsync;

    /// <summary>
    /// Callback for deleting a mod (returns true if deleted).
    /// MainWindow handles path resolution and actual deletion.
    /// </summary>
    public event Func<ModListItemViewModel, bool>? OnDeleteMod;

    /// <summary>
    /// Callback for installing a mod (returns true if installed).
    /// MainWindow handles release selection and installation logic.
    /// </summary>
    public event Func<ModListItemViewModel, Task<bool>>? OnInstallModAsync;

    /// <summary>
    /// Callback for updating a mod (returns true if updated).
    /// MainWindow handles the UpdateModsAsync logic.
    /// </summary>
    public event Func<ModListItemViewModel, Task<bool>>? OnUpdateModAsync;

    /// <summary>
    /// Callback for updating all mods.
    /// MainWindow handles the bulk update logic with dialogs.
    /// </summary>
    public event Func<Task>? OnUpdateAllModsAsync;

    /// <summary>
    /// Callback for fixing mod dependencies (returns true if fixed).
    /// MainWindow handles the complex dependency resolution logic.
    /// </summary>
    public event Func<ModListItemViewModel, Task<bool>>? OnFixModDependenciesAsync;
}
