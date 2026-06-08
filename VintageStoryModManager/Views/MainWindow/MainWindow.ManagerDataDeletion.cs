#nullable enable

using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Services;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void DeleteAllManagerFilesMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        const string confirmationMessage =
            "This will move every file Simple VS Manager created to the Recycle Bin, including its configuration folder, any ModData backups, cached mods, presets, and Firebase authentication tokens.\n\n" +
            "You can restore them from the Recycle Bin if needed. Continue?";

        var confirmation = WpfMessageBox.Show(
            this,
            confirmationMessage,
            "Simple VS Manager",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.OK) return;

        await ExecuteCloudOperationAsync(
            store => DeleteAllCloudModlistsAndAuthorizationAsync(store, false),
            "delete the Firebase user and cloud data");

        var dataDirectory = _dataDirectory;
        var deletionResult = await Task.Run(() => ManagerDataDeletionService.DeleteAllManagerFiles(dataDirectory)).ConfigureAwait(true);

        if (deletionResult.DeletedPaths.Count == 0 && deletionResult.FailedPaths.Count == 0)
        {
            WpfMessageBox.Show(
                this,
                "No Simple VS Manager files were found.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SwitchToInstalledModsTab();
            return;
        }

        var builder = new StringBuilder();

        if (deletionResult.DeletedPaths.Count > 0)
        {
            builder.AppendLine("Moved the following locations to the Recycle Bin:");
            foreach (var path in deletionResult.DeletedPaths) builder.AppendLine($"• {path}");
        }

        if (deletionResult.FailedPaths.Count == 0)
        {
            var message = builder.Length > 0
                ? builder.ToString()
                : "Finished moving Simple VS Manager files to the Recycle Bin.";

            WpfMessageBox.Show(
                this,
                message,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SwitchToInstalledModsTab();
            return;
        }

        if (builder.Length > 0) builder.AppendLine();

        builder.AppendLine(
            "The following locations could not be moved to the Recycle Bin. Please remove them manually:");
        foreach (var path in deletionResult.FailedPaths) builder.AppendLine($"• {path}");

        WpfMessageBox.Show(
            this,
            builder.ToString(),
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        SwitchToInstalledModsTab();
    }
}
