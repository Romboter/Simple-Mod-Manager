#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void ExperimentalModDebuggingMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedMod is not ModListItemViewModel selectedMod)
        {
            WpfMessageBox.Show(
                "Please select a mod before using Experimental Mod Debugging.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var modId = selectedMod.ModId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modId))
        {
            WpfMessageBox.Show(
                "The selected mod does not specify a mod ID to search for in the logs.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            WpfMessageBox.Show(
                "The manager data directory is not available. Please configure the Vintage Story data folder and try again.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var logsDirectory = Path.Combine(_dataDirectory, "Logs");
        if (!Directory.Exists(logsDirectory))
        {
            WpfMessageBox.Show(
                "No log files were found in the manager's Logs folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        List<ExperimentalModDebugLogLine> logLines;
        var busyScope = _viewModel?.EnterBusyScope();
        try
        {
            logLines = await Task.Run(() => ModDebugLogParser.CollectExperimentalModDebugLines(logsDirectory, modId))
                .ConfigureAwait(true);
        }
        finally
        {
            busyScope?.Dispose();
        }

        if (logLines.Count == 0)
            logLines.Add(ExperimentalModDebugLogLine.FromPlainText(
                $"No log entries referencing '{modId}' were found in client-debug, client-main, server-debug, or server-main logs."));

        var dialog = new ExperimentalModDebugDialog(modId, logLines)
        {
            Owner = this
        };

        _ = dialog.ShowDialog();
    }

    private async void ExperimentalAllModsDebuggingMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            WpfMessageBox.Show(
                "The installed mod list is not available. Please wait for the manager to finish loading and try again.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var installedMods = _viewModel.GetInstalledModsSnapshot();
        var modIdentifiers = new List<InstalledModLogIdentifier>();
        var seenModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in installedMods)
        {
            if (mod is null) continue;

            var modId = mod.ModId;
            if (string.IsNullOrWhiteSpace(modId)) continue;

            var trimmedModId = modId.Trim();
            if (!seenModIds.Add(trimmedModId)) continue;

            var displayName = mod.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName)) displayName = displayName.Trim();

            var displayLabel = string.IsNullOrWhiteSpace(displayName)
                ? trimmedModId
                : $"{displayName} ({trimmedModId})";

            modIdentifiers.Add(new InstalledModLogIdentifier(trimmedModId, displayLabel));
        }

        if (modIdentifiers.Count == 0)
        {
            WpfMessageBox.Show(
                "No installed mods with a valid mod ID were found to search for in the logs.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            WpfMessageBox.Show(
                "The manager data directory is not available. Please configure the Vintage Story data folder and try again.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var logsDirectory = Path.Combine(_dataDirectory, "Logs");
        if (!Directory.Exists(logsDirectory))
        {
            WpfMessageBox.Show(
                "No log files were found in the manager's Logs folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        List<ExperimentalModDebugLogLine> logLines;
        var busyScope = _viewModel.EnterBusyScope();
        try
        {
            await Task.Yield();
            logLines = await Task.Run(() => ModDebugLogParser.CollectInstalledModDebugLines(logsDirectory, modIdentifiers))
                .ConfigureAwait(true);
        }
        finally
        {
            busyScope.Dispose();
        }

        if (logLines.Count == 0)
            logLines.Add(ExperimentalModDebugLogLine.FromPlainText(
                "No log entries referencing the installed mods were found in client-debug, client-main, server-debug, or server-main logs."));

        var dialog = new ExperimentalModDebugDialog(
            "Log entries referencing installed mods",
            "No log entries referencing installed mods were found.",
            logLines)
        {
            Owner = this
        };

        _ = dialog.ShowDialog();
    }
}
