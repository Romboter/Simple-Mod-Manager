#nullable enable
using CommunityToolkit.Mvvm.Input;
using ModernWpf.Controls;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimpleVsManager.Cloud;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using UglyToad.PdfPig;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using YamlDotNet.Core;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ModUserReportChangedEventArgs = VintageStoryModManager.ViewModels.MainViewModel.ModUserReportChangedEventArgs;
using Colors = QuestPDF.Helpers.Colors;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursors = System.Windows.Input.Cursors;
using DataFolderBackupProgress = VintageStoryModManager.Services.DataBackupProgress;
using DataFolderBackupSummary = VintageStoryModManager.Services.DataBackupSummary;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using FileRecycleOption = Microsoft.VisualBasic.FileIO.RecycleOption;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;
using FileUIOption = Microsoft.VisualBasic.FileIO.UIOption;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Point = System.Windows.Point;
using ProgressBar = System.Windows.Controls.ProgressBar;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using VerticalAlignment = System.Windows.VerticalAlignment;
using WinForms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;
using WpfToolTip = System.Windows.Controls.ToolTip;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void RestoreBackupMenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        menuItem.Items.Clear();

        string directory;
        try
        {
            directory = EnsureBackupDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to access backup directory: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Backups unavailable",
                IsEnabled = false
            });
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Failed to enumerate backups: {0}", ex.Message);
            menuItem.Items.Add(new MenuItem
            {
                Header = "Backups unavailable",
                IsEnabled = false
            });
            return;
        }

        if (files.Length == 0)
        {
            menuItem.Items.Add(new MenuItem
            {
                Header = "No backups available",
                IsEnabled = false
            });
            return;
        }

        Array.Sort(files, (left, right) =>
            File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

        var appStartedAdded = false;

        foreach (var file in files)
        {
            var isAppStarted = BackupRetentionService.IsAppStartedBackup(file);
            if (isAppStarted)
            {
                if (appStartedAdded) continue;

                appStartedAdded = true;
            }

            var displayName = Path.GetFileNameWithoutExtension(file);
            var item = new MenuItem
            {
                Header = displayName,
                Tag = file
            };
            item.Click += RestoreBackupMenuItem_OnBackupClick;
            menuItem.Items.Add(item);
        }
    }

    private async void RestoreBackupMenuItem_OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string filePath) return;

        var confirmationDialog = new RestoreBackupDialog
        {
            Owner = this
        };

        var confirmation = confirmationDialog.ShowDialog();
        if (confirmation != true) return;

        await RestoreBackupAsync(filePath, confirmationDialog.RestoreConfigurations).ConfigureAwait(true);
    }

    private async Task RestoreBackupAsync(string backupPath, bool restoreConfigurations)
    {
        if (_viewModel is null) return;

        if (!File.Exists(backupPath))
        {
            WpfMessageBox.Show(
                "The selected backup could not be found.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PresetFileLoader.TryLoadPresetFromFile(backupPath,
                "Backup",
                ModListLoadOptions,
                out var preset,
                out var errorMessage))
        {
            var message = string.IsNullOrWhiteSpace(errorMessage)
                ? "The selected backup is not valid."
                : errorMessage!;
            WpfMessageBox.Show(
                $"Failed to restore the backup:\n{message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var loadedPreset = preset!;
        await ApplyPresetAsync(loadedPreset, restoreConfigurations).ConfigureAwait(true);
        _viewModel.ReportStatus($"Restored backup \"{loadedPreset.Name}\".");
    }

    private Task CreateAppStartedBackupAsync()
    {
        return CreateBackupAsync(
            "AppStarted",
            "Backup_AppStarted",
            false,
            true);
    }

    private async Task CreateBackupAsync(
            string trigger,
            string fallbackFileName,
            bool pruneAutomaticBackups,
            bool pruneAppStartedBackups)
    {
        if (_viewModel is null) return;

        await _backupSemaphore.WaitAsync().ConfigureAwait(true);
        try
        {
            var mods = _viewModel.GetInstalledModsSnapshot();
            var modCount = mods.Count;

            var timestamp = DateTime.Now;
            var formattedTimestamp =
                timestamp.ToString("dd MMM yyyy '•' HH.mm '•' ss's'", CultureInfo.InvariantCulture);

            var normalizedTrigger = string.IsNullOrWhiteSpace(trigger)
                ? "Automatic"
                : trigger.Trim();
            var modLabel = modCount == 1 ? "1 mod" : $"{modCount} mods";
            var displayName = $"{formattedTimestamp} -- {normalizedTrigger} ({modLabel})";

            string directory;
            try
            {
                directory = EnsureBackupDirectory();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Failed to prepare backup directory: {0}", ex.Message);
                return;
            }

            var fileName = FileNameHelper.SanitizeFileName(displayName, fallbackFileName);
            var filePath = Path.Combine(directory, $"{fileName}.json");

            var includedConfigurations =
                CaptureConfigurationsForBackup(mods);

            var serializable = PresetSnapshotBuilder.BuildSerializablePreset(
                _viewModel!.GetCurrentModStates(),
                displayName,
                true,
                true,
                includedConfigurations,
                ResolveGameVersion(null));

            var json =
                PdfModlistSerializer.SerializeToJson(serializable);

            try
            {
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Failed to write backup {0}: {1}", filePath, ex.Message);
                return;
            }

            if (pruneAutomaticBackups) BackupRetentionService.PruneAutomaticBackups(directory);

            if (pruneAppStartedBackups) BackupRetentionService.PruneAppStartedBackups(directory);
        }
        finally
        {
            _backupSemaphore.Release();
        }
    }

    private IReadOnlyDictionary<
            string,
            IReadOnlyList<ModConfigurationSnapshot>>?
            CaptureConfigurationsForBackup(
                IReadOnlyList<ModListItemViewModel> mods)
    {
        if (mods is null || mods.Count == 0)
            return null;

        var requests =
            mods
                .Where(mod =>
                    mod is not null &&
                    !string.IsNullOrWhiteSpace(mod.ModId))
                .GroupBy(
                    mod => mod.ModId.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var mod = group.First();
                    var modId = group.Key;

                    var configPaths =
                        _userConfiguration
                            .GetModConfigPaths(modId)
                            .Where(path =>
                                !string.IsNullOrWhiteSpace(path))
                            .Select(path => path.Trim())
                            .Where(File.Exists)
                            .ToList();

                    return new ModConfigurationCaptureRequest(
                        modId,
                        mod.DisplayName,
                        configPaths);
                })
                .Where(request =>
                    request.ConfigPaths.Count > 0)
                .ToList();

        var captureResult =
            ModConfigurationCaptureService.Capture(
                requests,
                _dataDirectory);

        foreach (var error in captureResult.Errors)
        {
            Trace.TraceWarning(
                "Failed to include configuration file {0} " +
                "for mod {1} in backup: {2}",
                error.Path,
                error.ModId,
                error.Message);
        }

        return captureResult.Configurations;
    }
}
