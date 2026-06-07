#nullable enable

using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private bool TryInitializePaths()
    {
        var dataResolved = TryResolveDataDirectory();
        var gameResolved = TryResolveGameDirectory();
        TryResolveCustomShortcut();

        if (dataResolved)
        {
            _userConfiguration.SetDataDirectory(_dataDirectory!);
            DeveloperProfileManager.UpdateOriginalProfile(_dataDirectory!);
        }

        if (gameResolved) _userConfiguration.SetGameDirectory(_gameDirectory!);

        return dataResolved && gameResolved;
    }

    private bool TryResolveDataDirectory()
    {
        var requiresSelection = _userConfiguration.RequiresDataDirectorySelection;
        var storedPath = _userConfiguration.DataDirectory;
        if (!requiresSelection && InstallationPathValidator.TryValidateDataDirectory(storedPath, out _dataDirectory, out _)) return true;

        if (!string.IsNullOrWhiteSpace(storedPath)) _userConfiguration.ClearDataDirectory();

        var defaultPath = DataDirectoryLocator.Resolve();
        if (!requiresSelection && InstallationPathValidator.TryValidateDataDirectory(defaultPath, out _dataDirectory, out _)) return true;

        var promptMessage = requiresSelection
            ? "Select the Vintage Story data folder for this profile to enable mod management."
            : "The Vintage Story data folder could not be located. Please select it to enable mod management.";

        WpfMessageBox.Show(promptMessage,
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        _dataDirectory = PromptForDirectory(
            "Select your VintagestoryData folder",
            _userConfiguration.DataDirectory ?? defaultPath,
            InstallationPathValidator.TryValidateDataDirectory,
            true);

        if (_dataDirectory is null)
        {
            WpfMessageBox.Show(
                "Mods cannot be managed until a VintagestoryData folder is selected. You can set it later from File > Set Data Folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private bool TryResolveGameDirectory()
    {
        var requiresSelection = _userConfiguration.RequiresGameDirectorySelection;
        var storedPath = _userConfiguration.GameDirectory;
        if (!requiresSelection && InstallationPathValidator.TryValidateGameDirectory(storedPath, out _gameDirectory, out _)) return true;

        if (!string.IsNullOrWhiteSpace(storedPath)) _userConfiguration.ClearGameDirectory();

        var defaultPath = GameDirectoryLocator.Resolve();
        if (!requiresSelection
            && !string.IsNullOrWhiteSpace(defaultPath)
            && InstallationPathValidator.TryValidateGameDirectory(defaultPath, out _gameDirectory, out _))
            return true;

        var promptMessage = requiresSelection
            ? "Select the Vintage Story installation folder for this profile to enable game-related features."
            : "The Vintage Story installation folder could not be located. Please select it to enable game-related features.";

        WpfMessageBox.Show(promptMessage,
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        _gameDirectory = PromptForDirectory(
            "Select your Vintage Story installation folder",
            _userConfiguration.GameDirectory ?? (string.IsNullOrWhiteSpace(defaultPath) ? null : defaultPath),
            InstallationPathValidator.TryValidateGameDirectory,
            true);

        if (_gameDirectory is null)
        {
            WpfMessageBox.Show(
                "Game-related features will be unavailable until a Vintage Story installation folder is selected. You can set it later from File > Set Game Folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private void TryResolveCustomShortcut()
    {
        var storedPath = _userConfiguration.CustomShortcutPath;
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            _customShortcutPath = null;
            return;
        }

        if (File.Exists(storedPath))
        {
            _customShortcutPath = storedPath;
            return;
        }

        _customShortcutPath = null;
        _userConfiguration.ClearCustomShortcutPath();

        WpfMessageBox.Show(
            "The previously selected Vintage Story shortcut could not be found and has been cleared.",
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
