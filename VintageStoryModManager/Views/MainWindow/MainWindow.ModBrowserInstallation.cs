#nullable enable

using System.Globalization;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void AddModToInstalledAndRemoveFromSearch(int modId)
    {
        if (_modBrowserViewModel == null) return;

        _modBrowserViewModel.AddInstalledMod(modId.ToString(CultureInfo.InvariantCulture), modId);

        // Remove the installed mod from the current search results
        var modToRemove = _modBrowserViewModel.ModsList.FirstOrDefault(m => m.ModId == modId);
        if (modToRemove != null)
        {
            _modBrowserViewModel.ModsList.Remove(modToRemove);

            // Clear selection if the removed mod was selected
            if (ReferenceEquals(_modBrowserViewModel.SelectedMod, modToRemove))
            {
                _modBrowserViewModel.SelectedMod = null;
            }
        }
    }

    private async Task InstallModFromBrowserAsync(DownloadableMod mod)
    {
        if (_isModUpdateInProgress)
            return;

        // Convert and validate the mod for installation
        var modViewModel = ConvertToModListItemViewModel(mod);

        await InstallModCoreAsync(modViewModel, logErrorOnException: true, postInstallSuccess: () =>
            AddModToInstalledAndRemoveFromSearch(mod.ModId));
    }

    private ModListItemViewModel ConvertToModListItemViewModel(DownloadableMod mod)
    {
        var entry = DownloadableModConverter.ToModEntry(mod);

        // Note: We use a dummy activation handler since this is only for installation
        return new ModListItemViewModel(
            entry,
            isActive: false,
            location: "Mod Database",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(false, null)),
            installedGameVersion: _viewModel?.InstalledGameVersion,
            isInstalled: false,
            shouldSkipVersion: null,
            requireExactVersionMatch: null);
    }
}
