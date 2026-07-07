#nullable enable

using System.IO;
using System.Text.Json;
using System.Windows;
using VintageStoryModManager.ViewModels;
using YamlDotNet.Core;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void EditConfigButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

        e.Handled = true;

        if (string.IsNullOrWhiteSpace(mod.ModId)) return;

        var configPaths = _userConfiguration
            .GetModConfigPaths(mod.ModId)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        try
        {
            for (var i = configPaths.Count - 1; i >= 0; i--)
            {
                var path = configPaths[i];
                if (File.Exists(path)) continue;

                configPaths.RemoveAt(i);
                _userConfiguration.RemoveModConfigPath(mod.ModId, path);
            }

            if (configPaths.Count == 0)
            {
                var primaryPath = PromptForConfigFile(mod, configPaths.FirstOrDefault());
                if (primaryPath is null) return;

                configPaths.Add(primaryPath);
                _userConfiguration.SetModConfigPaths(mod.ModId, configPaths);
                UpdateSelectedModEditConfigButton(mod);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            WpfMessageBox.Show($"Failed to store the configuration path:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            var editorViewModel = new ModConfigEditorViewModel(mod.DisplayName, configPaths);
            var editorWindow = new ModConfigEditorWindow(editorViewModel)
            {
                Owner = this
            };

            var result = editorWindow.ShowDialog();
            if (result == true)
            {
                var updatedPaths = editorViewModel.ConfigPaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();

                try
                {
                    if (updatedPaths.Count == 0)
                        _userConfiguration.RemoveModConfigPath(mod.ModId);
                    else
                        _userConfiguration.SetModConfigPaths(mod.ModId, updatedPaths);

                    UpdateSelectedModEditConfigButton(mod);
                    _viewModel?.ReportStatus($"Saved config for {mod.DisplayName}.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    WpfMessageBox.Show($"Failed to store the configuration path:\n{ex.Message}",
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or YamlException)
        {
            WpfMessageBox.Show($"Failed to open the configuration file:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _userConfiguration.RemoveModConfigPath(mod.ModId);
            UpdateSelectedModEditConfigButton(mod);
        }
    }
}
