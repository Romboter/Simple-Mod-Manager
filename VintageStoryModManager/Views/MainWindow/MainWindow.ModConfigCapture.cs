#nullable enable

using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private List<ModConfigOption> BuildModConfigOptions(bool selectByDefault = true)
    {
        if (_viewModel is null) return new List<ModConfigOption>();

        return ModConfigCaptureHelper.BuildOptions(
            _viewModel.GetInstalledModsSnapshot(),
            _userConfiguration.GetModConfigPaths,
            selectByDefault);
    }

    private async Task<Dictionary<string, IReadOnlyList<ModConfigurationSnapshot>>?>
        TryReadModConfigurationsAsync(
            IReadOnlyList<ModConfigOption> selectedConfigOptions)
    {
        var (configurations, errorMessage) =
            ModConfigCaptureHelper.CaptureConfigurations(selectedConfigOptions, _dataDirectory);

        if (errorMessage is not null)
        {
            await _confirmationService.NotifyAsync(
                    errorMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }

        return configurations;
    }
}
