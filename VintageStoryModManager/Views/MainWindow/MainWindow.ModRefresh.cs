#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task RefreshModsAsync(bool allowModDetailsRefresh = false)
    {
        if (_viewModel?.RefreshCommand == null) return;

        List<string>? selectedSourcePaths = null;
        string? anchorSourcePath = null;

        if (_selectedMods.Count > 0)
        {
            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            selectedSourcePaths = new List<string>(_selectedMods.Count);

            foreach (var selected in _selectedMods)
            {
                var sourcePath = selected.SourcePath;
                if (string.IsNullOrWhiteSpace(sourcePath)) continue;

                if (dedup.Add(sourcePath)) selectedSourcePaths.Add(sourcePath);
            }

            if (selectedSourcePaths.Count > 0 && _selectionAnchor is { } anchor) anchorSourcePath = anchor.SourcePath;
        }

        if (allowModDetailsRefresh) _viewModel.ForceNextRefreshToLoadDetails();

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        if (selectedSourcePaths is { Count: > 0 })
            RestoreSelectionFromSourcePaths(selectedSourcePaths, anchorSourcePath);

        // Keep ModBrowser in sync with installed mods
        SyncInstalledModsToModBrowser();
    }

    private void RefreshModDetailsOnly()
    {
        if (_viewModel == null) return;

        _viewModel.ForceNextRefreshToLoadDetails();
        _viewModel.RefreshInstalledModDetails();
    }

    private async Task RefreshModsWithErrorHandlingAsync()
    {
        if (_viewModel == null) return;

        if (Dispatcher.CheckAccess())
            await Dispatcher.Yield(DispatcherPriority.Background);
        else
            await Task.Yield();

        if (_userConfiguration.DisableAutoRefresh)
            _viewModel.EnableUserReportFetching(true);

        try
        {
            RefreshModDetailsOnly();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to refresh mod details:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static bool ShouldRefreshAfterDependencyResolution(string? statusMessage)
    {
        return !string.IsNullOrWhiteSpace(statusMessage)
               && statusMessage.StartsWith("Resolved dependencies for ", StringComparison.Ordinal);
    }

    private async Task RefreshModsAfterDependencyResolutionAsync()
    {
        if (_isDependencyResolutionRefreshPending) return;

        if (_viewModel?.RefreshCommand == null) return;

        _isDependencyResolutionRefreshPending = true;

        try
        {
            await RefreshModsAsync(true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"The mod list could not be refreshed after resolving dependencies:{Environment.NewLine}{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isDependencyResolutionRefreshPending = false;
        }
    }

    private void ScheduleRefreshAfterModlistLoadIfReady()
    {
        if (!_refreshAfterModlistLoadPending || _isRefreshingAfterModlistLoad) return;

        var viewModel = _viewModel;
        if (viewModel?.RefreshCommand == null) return;

        if (viewModel.IsLoadingMods || viewModel.IsLoadingModDetails) return;

        _isRefreshingAfterModlistLoad = true;

        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await RefreshModsAsync(true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    $"Failed to refresh mods after loading the modlist:{Environment.NewLine}{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _refreshAfterModlistLoadPending = false;
                _isRefreshingAfterModlistLoad = false;
            }
        }, DispatcherPriority.Background);
    }
}
