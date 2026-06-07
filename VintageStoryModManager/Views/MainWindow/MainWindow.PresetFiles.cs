#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private bool TrySaveSnapshot(
        string directory,
        string title,
        string filter,
        string folderWarningMessage,
        string fallbackName,
        Func<string?>? suggestedNameProvider,
        Action<string>? onSuccess,
        string failureContext,
        bool includeModVersions,
        bool exclusive,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations = null,
        Action<SerializablePreset>? configureSerializable = null)
    {
        if (_viewModel is null) return false;

        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = directory
        };

        dialog.FileOk += (_, args) =>
        {
            if (PathRelationshipHelper.IsPathWithinDirectory(directory, dialog.FileName)) return;

            WpfMessageBox.Show(folderWarningMessage,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Cancel = true;
        };

        var suggestedName = suggestedNameProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(suggestedName))
            dialog.FileName = FileNameHelper.BuildSuggestedFileName(suggestedName, fallbackName);

        var result = dialog.ShowDialog(this);
        if (result != true) return false;

        var filePath = dialog.FileName;
        var entryName = FileNameHelper.BuildSuggestedFileName(Path.GetFileNameWithoutExtension(filePath), fallbackName);
        if (!string.Equals(entryName, Path.GetFileNameWithoutExtension(filePath), StringComparison.Ordinal))
            filePath = Path.Combine(directory, entryName + ".json");

        var serializable = PresetSnapshotBuilder.BuildSerializablePreset(
            _viewModel!.GetCurrentModStates(),
            entryName,
            includeModVersions,
            exclusive,
            includedConfigurations,
            ResolveGameVersion(null));
        configureSerializable?.Invoke(serializable);

        try
        {
            var json =
                PdfModlistSerializer.SerializeToJson(serializable);
            File.WriteAllText(filePath, json);

            onSuccess?.Invoke(entryName);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WpfMessageBox.Show($"Failed to save the {failureContext}:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private string? ResolveGameVersion(string? requestedVersion)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion)) return requestedVersion.Trim();

        var installed = _viewModel?.InstalledGameVersion;
        return string.IsNullOrWhiteSpace(installed) ? null : installed!.Trim();
    }

    private void LoadPresetMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        for (var index = menuItem.Items.Count - 1; index >= 1; index--) menuItem.Items.RemoveAt(index);

        string directory;
        try
        {
            directory = EnsurePresetDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to access preset directory: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Presets unavailable",
                IsEnabled = false,
                Focusable = false,
                Opacity = 0.75
            });
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to enumerate presets: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Presets unavailable",
                IsEnabled = false,
                Focusable = false,
                Opacity = 0.75
            });
            return;
        }

        if (files.Length == 0)
        {
            menuItem.Items.Add(new MenuItem
            {
                Header = "No presets available",
                IsEnabled = false,
                Focusable = false,
                Opacity = 0.75
            });
            return;
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        menuItem.Items.Add(new Separator());

        foreach (var file in files)
        {
            var displayName = Path.GetFileNameWithoutExtension(file);
            var item = new MenuItem
            {
                Header = displayName,
                Tag = file
            };
            item.Click += LoadPresetMenuItem_OnPresetClick;
            menuItem.Items.Add(item);
        }
    }

    private async void LoadPresetMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var presetDirectory = EnsurePresetDirectory();
        var dialog = new OpenFileDialog
        {
            Title = "Load Mod Preset",
            Filter = "Preset files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            InitialDirectory = presetDirectory,
            Multiselect = false
        };

        dialog.FileOk += (_, args) =>
        {
            if (PathRelationshipHelper.IsPathWithinDirectory(presetDirectory, dialog.FileName)) return;

            WpfMessageBox.Show("Please select a preset from the Presets folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Cancel = true;
        };

        var result = dialog.ShowDialog(this);
        if (result != true) return;

        await LoadPresetFromFileAsync(dialog.FileName).ConfigureAwait(true);
    }

    private async void LoadPresetMenuItem_OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string filePath) return;

        await LoadPresetFromFileAsync(filePath).ConfigureAwait(true);
    }

    private async Task LoadPresetFromFileAsync(string filePath)
    {
        if (_viewModel is null) return;

        if (!File.Exists(filePath))
        {
            WpfMessageBox.Show(
                "The selected preset could not be found.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PresetFileLoader.TryLoadPresetFromFile(filePath, "Preset", StandardPresetLoadOptions, out var preset, out var errorMessage))
        {
            var message = string.IsNullOrWhiteSpace(errorMessage)
                ? "The selected file is not a valid preset."
                : errorMessage!;
            WpfMessageBox.Show($"Failed to load the preset:\n{message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var loadedPreset = preset!;
        _userConfiguration.SetLastSelectedPresetName(loadedPreset.Name);
        await ApplyPresetAsync(loadedPreset).ConfigureAwait(true);
        _viewModel?.ReportStatus($"Loaded preset \"{loadedPreset.Name}\".");
    }

    private void SavePresetMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var presetDirectory = EnsurePresetDirectory();
        TrySaveSnapshot(
            presetDirectory,
            "Save Mod Preset",
            "Preset files (*.json)|*.json|All files (*.*)|*.*",
            "Presets must be saved inside the Presets folder.",
            "Preset",
            () => _userConfiguration.GetLastSelectedPresetName(),
            name =>
            {
                _userConfiguration.SetLastSelectedPresetName(name);
                _viewModel?.ReportStatus($"Saved preset \"{name}\".");
            },
            "preset",
            false,
            false);
    }
}
