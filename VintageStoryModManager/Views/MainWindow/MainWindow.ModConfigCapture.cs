#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var options = new List<ModConfigOption>();

        if (_viewModel is null) return options;

        var mods = _viewModel.GetInstalledModsSnapshot();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            if (mod is null || string.IsNullOrWhiteSpace(mod.ModId)) continue;

            var normalizedId = mod.ModId.Trim();
            if (!seenIds.Add(normalizedId)) continue;

            var configPaths = _userConfiguration.GetModConfigPaths(normalizedId)
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .ToList();

            if (configPaths.Count > 0)
                options.Add(new ModConfigOption(normalizedId, mod.DisplayName, configPaths, selectByDefault));
        }

        options.Sort((left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        return options;
    }

    private Dictionary<string, IReadOnlyList<ModConfigurationSnapshot>>?
        TryReadModConfigurations(
            IReadOnlyList<ModConfigOption> selectedConfigOptions)
    {
        var requests =
            selectedConfigOptions
                .Where(option => option is not null)
                .Select(option =>
                    new ModConfigurationCaptureRequest(
                        option.ModId,
                        option.DisplayName,
                        option.ConfigPaths))
                .ToList();

        var captureResult =
            ModConfigurationCaptureService.Capture(
                requests,
                _dataDirectory);

        if (captureResult.Errors.Count > 0)
        {
            WpfMessageBox.Show(
                "Some configuration files could not be included:\n" +
                string.Join(
                    "\n",
                    captureResult.Errors.Select(
                        error =>
                            $"{error.DisplayName}: {error.Message}")),
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return captureResult.Configurations is null
            ? null
            : new Dictionary<
                string,
                IReadOnlyList<ModConfigurationSnapshot>>(
                    captureResult.Configurations,
                    StringComparer.OrdinalIgnoreCase);
    }
}
