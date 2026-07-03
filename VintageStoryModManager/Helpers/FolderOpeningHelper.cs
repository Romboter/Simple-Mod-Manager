#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Helpers;

internal static class FolderOpeningHelper
{
    internal static void OpenFolder(string? path, string description)
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

    internal static void OpenFolderWithShell(string path)
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
