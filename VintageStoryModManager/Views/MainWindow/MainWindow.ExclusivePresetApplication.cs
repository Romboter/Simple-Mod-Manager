#nullable enable

using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task ApplyExclusivePresetAsync(ModPreset preset)
    {
        if (_viewModel?.ModsView is null) return;

        if (preset.ModStates.Count == 0) return;

        var keepSet = new HashSet<string>(
            preset.ModStates.Select(state => state.ModId),
            StringComparer.OrdinalIgnoreCase);

        if (keepSet.Count == 0) return;

        var installedMods = _viewModel.ModsView.Cast<ModListItemViewModel>().ToList();
        if (installedMods.Count == 0) return;

        var failures = new List<string>();
        var removedCount = 0;
        string? localBackupSessionDirectory = null;
        List<string>? backedUpModNames = null;
        var localBackupInitializationFailed = false;

        foreach (var mod in installedMods)
        {
            if (!mod.IsInstalled) continue;

            if (keepSet.Contains(mod.ModId)) continue;

            if (!TryGetManagedModPath(mod, out var modPath, out var errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage)) failures.Add($"{mod.DisplayName}: {errorMessage}");

                continue;
            }

            var sourceExists = Directory.Exists(modPath) || File.Exists(modPath);
            if (!mod.HasModDatabasePageLink && sourceExists && !localBackupInitializationFailed)
            {
                if (string.IsNullOrWhiteSpace(localBackupSessionDirectory))
                    try
                    {
                        localBackupSessionDirectory = CreateLocalModBackupSessionDirectory();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                                   or PathTooLongException)
                    {
                        failures.Add($"Failed to prepare the local mod backup directory: {ex.Message}");
                        localBackupInitializationFailed = true;
                    }

                if (!localBackupInitializationFailed && !string.IsNullOrWhiteSpace(localBackupSessionDirectory))
                    try
                    {
                        var entryDirectory = GetLocalModBackupEntryDirectory(localBackupSessionDirectory, mod);
                        LocalModBackupService.BackupLocalModAtPath(modPath, entryDirectory);

                        var name = string.IsNullOrWhiteSpace(mod.DisplayName)
                            ? mod.ModId
                            : mod.DisplayName;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            backedUpModNames ??= new List<string>();
                            backedUpModNames.Add(name.Trim());
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                                   or PathTooLongException)
                    {
                        failures.Add(
                            $"{mod.DisplayName}: Failed to backup the local copy before deletion — {ex.Message}");
                    }
            }

            try
            {
                if (Directory.Exists(modPath))
                {
                    Directory.Delete(modPath, true);
                    removedCount++;
                }
                else if (File.Exists(modPath))
                {
                    File.Delete(modPath);
                    removedCount++;
                }
                else
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{mod.DisplayName}: {ex.Message}");
                continue;
            }

            _userConfiguration.RemoveModConfigPath(mod.ModId, true);
        }

        if (!string.IsNullOrWhiteSpace(localBackupSessionDirectory) && backedUpModNames is { Count: > 0 })
        {
            _recentLocalModBackupDirectory = localBackupSessionDirectory;
            _recentLocalModBackupModNames = backedUpModNames;
        }

        if (removedCount > 0)
        {
            await RefreshModsAsync(true).ConfigureAwait(true);

            var status = removedCount == 1
                ? "Removed 1 mod not in the preset."
                : $"Removed {removedCount} mods not in the preset.";
            _viewModel?.ReportStatus(status);
        }

        if (failures.Count > 0)
        {
            await _confirmationService.NotifyAsync(
                    PresetDialogTextBuilder.BuildExclusiveRemovalFailureMessage(failures),
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
    }
}
