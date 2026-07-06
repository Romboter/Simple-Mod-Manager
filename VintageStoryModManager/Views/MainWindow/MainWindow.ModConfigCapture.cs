#nullable enable

using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

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

    private Dictionary<string, IReadOnlyList<ModConfigurationSnapshot>>?
        TryReadModConfigurations(
            IReadOnlyList<ModConfigOption> selectedConfigOptions)
    {
        var (configurations, errorMessage) =
            ModConfigCaptureHelper.CaptureConfigurations(selectedConfigOptions, _dataDirectory);

        if (errorMessage is not null)
        {
            WpfMessageBox.Show(
                errorMessage,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return configurations;
    }
}
