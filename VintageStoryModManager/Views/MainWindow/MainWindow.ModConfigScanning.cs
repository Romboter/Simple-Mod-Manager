#nullable enable

using System.Windows;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void ScanForModConfigsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var blockedMessage = ModConfigDiscoveryService.GetScanBlockedMessage(
            _viewModel is not null,
            _viewModel?.IsBusy == true,
            _dataDirectory);
        if (blockedMessage is not null)
        {
            await _confirmationService.NotifyAsync(
                    blockedMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var viewModel = _viewModel!;

        try
        {
            var candidates = new List<(string? ModId, string? DisplayName)>();
            foreach (var mod in viewModel.GetInstalledModsSnapshot())
            {
                if (mod is null) continue;

                candidates.Add((mod.ModId, mod.DisplayName));
            }

            var results = await ModConfigDiscoveryService.ScanAsync(
                    _dataDirectory,
                    candidates,
                    id => _userConfiguration.TryGetModConfigPath(id, out var path) ? path : null,
                    (id, path) => _userConfiguration.SetModConfigPath(id, path))
                .ConfigureAwait(true);

            if (results.Count > 0) UpdateSelectedModEditConfigButton(viewModel.SelectedMod);

            if (results.Count == 0)
            {
                viewModel.ReportStatus("No missing mod configuration files were found.");
                await _confirmationService.NotifyAsync(
                        "No missing mod configuration files were found.",
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);
                return;
            }

            viewModel.ReportStatus($"Assigned configuration files for {results.Count} mod(s).");

            await _confirmationService.NotifyAsync(
                    ModConfigDiscoveryService.FormatAssignedConfigsMessage(results),
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    ModConfigDialogTextBuilder.BuildScanFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }
}
