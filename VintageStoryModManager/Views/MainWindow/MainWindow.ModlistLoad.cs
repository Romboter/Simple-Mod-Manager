#nullable enable

using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox =
    VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private ModlistLoadMode? PromptModlistLoadMode()
        {
            var behavior = _userConfiguration.ModlistAutoLoadBehavior;
            switch (behavior)
            {
                case ModlistAutoLoadBehavior.Replace:
                    return ModlistLoadMode.Replace;
                case ModlistAutoLoadBehavior.Add:
                    return ModlistLoadMode.Add;
            }

            var buttonOverrides = new MessageDialogButtonContentOverrides
            {
                Yes = "Only Modlist mods",
                No = "Add Modlist mods"
            };

            var result = WpfMessageBox.Show(
                this,
                "How would you like to load the modlist?" +
                "\n\nOnly Modlist mods: Delete your current mods and install only the mods from the modlist." +
                "\nAdd Modlist mods: Keep your current mods and add any missing mods from the modlist." +
                "\nCancel: Do nothing.",
                "Load Modlist",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                buttonContentOverrides: buttonOverrides);

            return result switch
            {
                MessageBoxResult.Yes => ModlistLoadMode.Replace,
                MessageBoxResult.No => ModlistLoadMode.Add,
                _ => null
            };
        }

    private PresetLoadOptions GetModlistLoadOptions(ModlistLoadMode mode)
        {
            if (mode == ModlistLoadMode.Replace) return ModListLoadOptions;

            return new PresetLoadOptions(ModListLoadOptions.ApplyModStatus, ModListLoadOptions.ApplyModVersions, false);
        }

    private bool EnsureModlistBackupBeforeLoad()
        {
            MessageBoxResult prompt;
            if (_userConfiguration.SuppressModlistSavePrompt)
            {
                prompt = MessageBoxResult.No;
            }
            else
            {
                var suppressButton = new MessageDialogExtraButton(
                    "No, don't ask again",
                    MessageBoxResult.No,
                    () => _userConfiguration.SetSuppressModlistSavePrompt(true));

                prompt = WpfMessageBox.Show(
                    "Would you like to backup your current mods as a Modlist before loading the selected Modlist? Your current mods will be deleted! ",
                    "Simple VS Manager",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    suppressButton);
            }

            if (prompt == MessageBoxResult.Cancel) return false;

            if (prompt == MessageBoxResult.Yes)
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
