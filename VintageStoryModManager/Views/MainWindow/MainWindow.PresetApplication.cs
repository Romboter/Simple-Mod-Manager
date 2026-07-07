#nullable enable

using VintageStoryModManager.Models;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task ApplyPresetAsync(ModPreset preset, bool importConfigurations = true)
    {
        var viewModel = _viewModel;
        if (viewModel is null || _isApplyingPreset) return;

        using var busyScope = viewModel.EnterBusyScope();

        _recentLocalModBackupDirectory = null;
        _recentLocalModBackupModNames = null;

        var scheduleRefreshAfterLoad = false;
        _isApplyingPreset = true;
        UpdateModlistLoadingUiState();
        try
        {
            if (preset.IncludesModVersions && preset.ModStates.Count > 0)
                scheduleRefreshAfterLoad = await ApplyPresetModVersionsAsync(preset).ConfigureAwait(true);

            var applied = await viewModel.ApplyPresetAsync(preset).ConfigureAwait(true);
            if (applied)
            {
                if (preset.IsExclusive) await ApplyExclusivePresetAsync(preset).ConfigureAwait(true);

                viewModel.SelectedSortOption?.Apply(viewModel.ModsView);
                viewModel.ModsView.Refresh();
            }

            if (importConfigurations) await ImportPresetConfigsAsync(preset).ConfigureAwait(true);
        }
        finally
        {
            _isApplyingPreset = false;
            UpdateModlistLoadingUiState();

            if (scheduleRefreshAfterLoad)
            {
                _refreshAfterModlistLoadPending = true;
                ScheduleRefreshAfterModlistLoadIfReady();
            }
        }
    }
}
