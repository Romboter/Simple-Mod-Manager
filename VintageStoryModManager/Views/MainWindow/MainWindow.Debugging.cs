#nullable enable
using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void ExperimentalModDebuggingMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedMod is not ModListItemViewModel selectedMod)
        {
            await _confirmationService.NotifyAsync(
                    "Please select a mod before using Experimental Mod Debugging.",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var modId = selectedMod.ModId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modId))
        {
            await _confirmationService.NotifyAsync(
                    "The selected mod does not specify a mod ID to search for in the logs.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            await _confirmationService.NotifyAsync(
                    "The manager data directory is not available. Please configure the Vintage Story data folder and try again.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        var logsDirectory = Path.Combine(_dataDirectory, "Logs");
        if (!Directory.Exists(logsDirectory))
        {
            await _confirmationService.NotifyAsync(
                    "No log files were found in the manager's Logs folder.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    "The installed mod list is not available. Please wait for the manager to finish loading and try again.",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var installedMods = _viewModel.GetInstalledModsSnapshot();
        var modIdentifiers = InstalledModLogIdentifierListBuilder.Build(
            installedMods.Select(mod => (mod?.ModId, mod?.DisplayName)));

        if (modIdentifiers.Count == 0)
        {
            await _confirmationService.NotifyAsync(
                    "No installed mods with a valid mod ID were found to search for in the logs.",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            await _confirmationService.NotifyAsync(
                    "The manager data directory is not available. Please configure the Vintage Story data folder and try again.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        var logsDirectory = Path.Combine(_dataDirectory, "Logs");
        if (!Directory.Exists(logsDirectory))
        {
            await _confirmationService.NotifyAsync(
                    "No log files were found in the manager's Logs folder.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
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
