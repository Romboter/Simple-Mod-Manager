#nullable enable

using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void OpenModFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        FolderOpeningHelper.OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Mods"), "mods");
    }

    private void OpenConfigFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        FolderOpeningHelper.OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "ModConfig"), "config");
    }

    private void OpenLogsFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        FolderOpeningHelper.OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Logs"), "logs");
    }
}
