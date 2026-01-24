using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for Firebase cloud modlist operations (save, load, delete, refresh).
/// Extracted from MainWindow.xaml.cs Phase 3.
/// Follows callback pattern for MainWindow coordination.
/// </summary>
public partial class CloudModlistViewModel : ObservableObject
{
    private readonly Window _owner;
    private readonly UserConfigurationService _userConfiguration;

    [ObservableProperty]
    private bool _isCloudRefreshInProgress;

    [ObservableProperty]
    private CloudModlistListEntry? _selectedCloudModlist;

    public CloudModlistViewModel(
        Window owner,
        UserConfigurationService userConfiguration)
    {
        _owner = owner;
        _userConfiguration = userConfiguration;
    }

    /// <summary>
    /// Saves the current mod setup as a cloud modlist to Firebase.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteCloudOperation))]
    private async Task SaveToCloudAsync()
    {
        try
        {
            var saved = OnSaveToCloudAsync != null ? await OnSaveToCloudAsync.Invoke() : false;

            if (saved)
            {
                // Request cloud modlists refresh if viewing modlist tab
                if (OnRequestCloudRefresh != null) await OnRequestCloudRefresh.Invoke(true);
                OnReportStatus?.Invoke("Saved modlist to the cloud.", false);
            }
        }
        catch (Exception ex)
        {
            ModManagerMessageBox.Show(
                $"Failed to save modlist to cloud:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Loads a cloud modlist from Firebase and applies it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstallCloudModlist))]
    private async Task LoadFromCloudAsync(CloudModlistListEntry? entry)
    {
        entry ??= SelectedCloudModlist;
        if (entry is null) return;

        try
        {
            var loaded = OnLoadFromCloudAsync != null ? await OnLoadFromCloudAsync.Invoke(entry) : false;

            if (loaded)
            {
                OnReportStatus?.Invoke($"Loaded cloud modlist \"{entry.DisplayName}\".", false);
            }
        }
        catch (Exception ex)
        {
            ModManagerMessageBox.Show(
                $"Failed to load cloud modlist:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Deletes a cloud modlist from Firebase.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteCloudModlist))]
    private async Task DeleteCloudModlistAsync(CloudModlistListEntry? entry)
    {
        entry ??= SelectedCloudModlist;
        if (entry is null) return;

        var confirmation = ModManagerMessageBox.Show(
            $"Are you sure you want to delete the cloud modlist \"{entry.DisplayName}\"? This cannot be undone.",
            "Delete Cloud Modlist",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            var deleted = OnDeleteCloudModlistAsync != null ? await OnDeleteCloudModlistAsync.Invoke(entry) : false;

            if (deleted)
            {
                OnReportStatus?.Invoke($"Deleted cloud modlist \"{entry.DisplayName}\".", false);

                // Request cloud modlists refresh
                if (OnRequestCloudRefresh != null) await OnRequestCloudRefresh.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            ModManagerMessageBox.Show(
                $"Failed to delete cloud modlist:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Refreshes the list of cloud modlists from Firebase.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteCloudOperation))]
    private async Task RefreshCloudModlistsAsync(bool force = false)
    {
        if (IsCloudRefreshInProgress) return;

        IsCloudRefreshInProgress = true;

        try
        {
            if (OnRefreshCloudModlistsAsync != null)
            {
                await OnRefreshCloudModlistsAsync.Invoke(force);
            }
        }
        catch (Exception ex)
        {
            ModManagerMessageBox.Show(
                $"Failed to refresh cloud modlists:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsCloudRefreshInProgress = false;
        }
    }

    /// <summary>
    /// Opens the cloud modlist management dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteCloudOperation))]
    private async Task ManageCloudModlistsAsync()
    {
        try
        {
            if (OnManageCloudModlistsAsync != null)
            {
                await OnManageCloudModlistsAsync.Invoke();
            }
        }
        catch (Exception ex)
        {
            ModManagerMessageBox.Show(
                $"Failed to manage cloud modlists:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanExecuteCloudOperation() => !IsCloudRefreshInProgress;

    private bool CanInstallCloudModlist() => !IsCloudRefreshInProgress && SelectedCloudModlist is not null;

    private bool CanDeleteCloudModlist() => !IsCloudRefreshInProgress && SelectedCloudModlist is not null;

    /// <summary>
    /// Callback for reporting status messages.
    /// </summary>
    public event Action<string, bool>? OnReportStatus;

    /// <summary>
    /// Callback for saving current mod setup to cloud.
    /// MainWindow handles dialog, modlist building, and Firebase save operation.
    /// Returns true if saved successfully.
    /// </summary>
    public event Func<Task<bool>>? OnSaveToCloudAsync;

    /// <summary>
    /// Callback for loading a cloud modlist.
    /// MainWindow handles content download, dialog, and modlist loading.
    /// Returns true if loaded successfully.
    /// </summary>
    public event Func<CloudModlistListEntry, Task<bool>>? OnLoadFromCloudAsync;

    /// <summary>
    /// Callback for deleting a cloud modlist.
    /// MainWindow handles Firebase deletion.
    /// Returns true if deleted successfully.
    /// </summary>
    public event Func<CloudModlistListEntry, Task<bool>>? OnDeleteCloudModlistAsync;

    /// <summary>
    /// Callback for refreshing cloud modlists.
    /// MainWindow handles Firebase query and ViewModel update.
    /// </summary>
    public event Func<bool, Task>? OnRefreshCloudModlistsAsync;

    /// <summary>
    /// Callback for managing cloud modlists (rename, delete dialog).
    /// MainWindow handles the management dialog.
    /// </summary>
    public event Func<Task>? OnManageCloudModlistsAsync;

    /// <summary>
    /// Callback for requesting cloud modlists refresh (after save/delete).
    /// </summary>
    public event Func<bool, Task>? OnRequestCloudRefresh;
}
