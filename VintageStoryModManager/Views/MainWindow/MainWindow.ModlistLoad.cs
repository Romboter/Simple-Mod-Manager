#nullable enable

using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task<ModlistLoadMode?> PromptModlistLoadModeAsync()
    {
        var behavior = _userConfiguration.ModlistAutoLoadBehavior;
        switch (behavior)
        {
            case ModlistAutoLoadBehavior.Replace:
                return ModlistLoadMode.Replace;
            case ModlistAutoLoadBehavior.Add:
                return ModlistLoadMode.Add;
        }

        var result = await _confirmationService.ConfirmThreeWayAsync(
                "How would you like to load the modlist?" +
                "\n\nOnly Modlist mods: Delete your current mods and install only the mods from the modlist." +
                "\nAdd Modlist mods: Keep your current mods and add any missing mods from the modlist." +
                "\nCancel: Do nothing.",
                "Load Modlist",
                DialogSeverity.Question,
                yesText: "Only Modlist mods",
                noText: "Add Modlist mods")
            .ConfigureAwait(true);

        return result switch
        {
            ThreeWayConfirmResult.Yes => ModlistLoadMode.Replace,
            ThreeWayConfirmResult.No => ModlistLoadMode.Add,
            _ => null
        };
    }

    private PresetLoadOptions GetModlistLoadOptions(ModlistLoadMode mode)
    {
        if (mode == ModlistLoadMode.Replace) return ModListLoadOptions;

        return new PresetLoadOptions(ModListLoadOptions.ApplyModStatus, ModListLoadOptions.ApplyModVersions, false);
    }

    private async Task<bool> EnsureModlistBackupBeforeLoadAsync()
    {
        ThreeWayConfirmResult prompt;
        if (_userConfiguration.SuppressModlistSavePrompt)
        {
            prompt = ThreeWayConfirmResult.No;
        }
        else
        {
            var suppressOption = new SuppressibleConfirmOption(
                "No, don't ask again",
                () => _userConfiguration.SetSuppressModlistSavePrompt(true));

            prompt = await _confirmationService.ConfirmThreeWayAsync(
                    "Would you like to backup your current mods as a Modlist before loading the selected Modlist? Your current mods will be deleted! ",
                    "Simple VS Manager",
                    DialogSeverity.Question,
                    suppressOption: suppressOption)
                .ConfigureAwait(true);
        }

        if (prompt == ThreeWayConfirmResult.Cancel) return false;

        if (prompt == ThreeWayConfirmResult.Yes)
        {
            var result = TrySaveModlist(null, out var savedFilePath);
            if (result)
            {
                if (!string.IsNullOrWhiteSpace(savedFilePath))
                    RefreshLocalModlists(true, new[] { savedFilePath });
                else
                    RefreshLocalModlists(true);
            }

            return result;
        }

        return true;
    }

    private Task CreateAutomaticBackupAsync(string trigger)
    {
        return CreateBackupAsync(
            trigger,
            "Backup",
            true,
            false);
    }
}
