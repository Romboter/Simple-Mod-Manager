#nullable enable

using System;
using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using WinForms = System.Windows.Forms;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private string? PromptForDirectory(string description, string? initialPath, PathValidator validator,
            bool allowCancel)
    {
        var candidate = initialPath;

        while (true)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate)) dialog.SelectedPath = candidate;

            var result = dialog.ShowDialog();
            if (result != WinForms.DialogResult.OK)
            {
                if (allowCancel) return null;

                var exit = WpfMessageBox.Show(
                    "You must select a folder to continue. Do you want to exit the application?",
                    "Simple VS Manager",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (exit == MessageBoxResult.Yes) return null;

                continue;
            }

            candidate = dialog.SelectedPath;
            if (validator(candidate, out var normalized, out var errorMessage)) return normalized;

            WpfMessageBox.Show(errorMessage ?? "The selected folder is not valid.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private string? PromptForConfigFile(ModListItemViewModel mod, string? previousPath)
    {
        var initialDirectory = GetInitialConfigDirectory(previousPath);

        using var dialog = new WinForms.OpenFileDialog
        {
            Title = $"Select config file for {mod.DisplayName}",
            Filter = "Config files (*.json;*.yaml;*.yml)|*.json;*.yaml;*.yml|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        if (!string.IsNullOrWhiteSpace(previousPath)) dialog.FileName = Path.GetFileName(previousPath);

        var result = dialog.ShowDialog();
        if (result != WinForms.DialogResult.OK) return null;

        try
        {
            return Path.GetFullPath(dialog.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The selected configuration file path is invalid.", nameof(previousPath), ex);
        }
    }

    private string? GetInitialConfigDirectory(string? previousPath)
    {
        if (!string.IsNullOrWhiteSpace(previousPath))
            try
            {
                var directory = Path.GetDirectoryName(previousPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) return directory;
            }
            catch (Exception)
            {
                // Ignore invalid stored paths and fall back to the default directory.
            }

        if (!string.IsNullOrWhiteSpace(_dataDirectory))
        {
            var configDirectory = Path.Combine(_dataDirectory, "ModConfig");
            if (Directory.Exists(configDirectory)) return configDirectory;

            return _dataDirectory;
        }

        return null;
    }
}
