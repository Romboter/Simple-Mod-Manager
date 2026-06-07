#nullable enable

using System.Runtime.InteropServices;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WinForms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void SelectedModCopyForServerButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

        var command = ServerCommandBuilder.TryBuildInstallCommand(mod.ModId, mod.Version);
        if (string.IsNullOrWhiteSpace(command)) return;

        try
        {
            WinForms.Clipboard.SetDataObject(command, true, 10, 100);
            var trimmedCommand = command.Trim();
            var statusMessage = $"Copied {trimmedCommand}";
            _viewModel?.ReportStatus(statusMessage);
        }
        catch (ExternalException ex)
        {
            var errorMessage = $"Failed to copy server install command for {mod.DisplayName}: {ex.Message}";
            _viewModel?.ReportStatus(errorMessage, true);
            WpfMessageBox.Show(
                this,
                "Failed to copy the server install command. Please try again.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
