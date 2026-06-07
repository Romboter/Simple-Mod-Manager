#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void OpenModFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Mods"), "mods");
    }

    private void OpenConfigFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "ModConfig"), "config");
    }

    private void OpenLogsFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Logs"), "logs");
    }

    private static void OpenFolder(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            WpfMessageBox.Show(
                $"The {description} folder is not available. Please verify the VintagestoryData folder from File > Set Data Folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(path))
        {
            WpfMessageBox.Show(
                $"The {description} folder could not be found at:\n{path}\nPlease verify the VintagestoryData folder from File > Set Data Folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            OpenFolderWithShell(path);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to open the {description} folder:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OpenFolderWithShell(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var explorerStartInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            };
            explorerStartInfo.ArgumentList.Add(path);
            Process.Start(explorerStartInfo);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
