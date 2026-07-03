#nullable enable
using System.Diagnostics;
using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using WinForms = System.Windows.Forms;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async Task<bool> TryEnsureDataBackupBeforeLaunchAsync()
    {
        if (!_userConfiguration.AutomaticDataBackupsEnabled) return true;
        if (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory)) return true;

        ShowDataBackupOverlay("Preparing VintagestoryData backup...");
        var progress = CreateDataBackupProgressReporter("Backing up VintagestoryData...");

        var installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion(_gameDirectory);

        try
        {
            await _dataBackupService
                .CreateBackupAsync(_dataDirectory!, installedGameVersion, progress, CancellationToken.None)
                .ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var response = WpfMessageBox.Show(
                $"The automatic VintagestoryData backup failed:\n{ex.Message}\n\nLaunch Vintage Story without creating a backup?",
                "Simple VS Manager",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return response == MessageBoxResult.Yes;
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"The automatic VintagestoryData backup failed:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        finally
        {
            HideDataBackupOverlay();
        }
    }

    private async void LaunchGameButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreset) return;

        if (!await TryEnsureDataBackupBeforeLaunchAsync().ConfigureAwait(true)) return;

        if (!string.IsNullOrWhiteSpace(_customShortcutPath))
        {
            if (!File.Exists(_customShortcutPath))
            {
                WpfMessageBox.Show(
                    "The custom Vintage Story shortcut could not be found. Please set it again from File > Set custom Vintage Story shortcut.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _userConfiguration.ClearCustomShortcutPath();
                _customShortcutPath = null;
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _customShortcutPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Failed to launch Vintage Story using the shortcut:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory))
        {
            WpfMessageBox.Show(
                "The VintagestoryData folder could not be located. Please verify it from File > Set Data Folder before launching the game.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var executable = GameDirectoryLocator.FindExecutable(_gameDirectory);
        if (executable is null)
        {
            WpfMessageBox.Show(
                "The Vintage Story executable could not be found. Verify the game folder in File > Set Game Folder.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--dataPath");
            startInfo.ArgumentList.Add(_dataDirectory);

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to launch Vintage Story:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SetCustomShortcutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        string? initialDirectory = null;
        string? initialFileName = null;

        if (!string.IsNullOrWhiteSpace(_customShortcutPath))
            try
            {
                initialDirectory = Path.GetDirectoryName(_customShortcutPath);
                initialFileName = Path.GetFileName(_customShortcutPath);
            }
            catch (Exception)
            {
                initialDirectory = null;
                initialFileName = null;
            }

        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "Select Vintage Story shortcut",
            Filter = "Shortcut files (*.lnk)|*.lnk|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        if (!string.IsNullOrWhiteSpace(initialFileName)) dialog.FileName = initialFileName;

        var result = dialog.ShowDialog();
        if (result == WinForms.DialogResult.OK)
        {
            var selected = dialog.FileName;
            if (!File.Exists(selected))
            {
                WpfMessageBox.Show(
                    "The selected shortcut could not be found.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _userConfiguration.SetCustomShortcutPath(selected);
                _customShortcutPath = _userConfiguration.CustomShortcutPath;
            }
            catch (ArgumentException ex)
            {
                WpfMessageBox.Show(
                    $"The selected shortcut is not valid:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_customShortcutPath)) return;

        var clear = WpfMessageBox.Show(
            "Do you want to clear the custom Vintage Story shortcut?",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (clear != MessageBoxResult.Yes) return;

        _userConfiguration.ClearCustomShortcutPath();
        _customShortcutPath = null;
    }
}
