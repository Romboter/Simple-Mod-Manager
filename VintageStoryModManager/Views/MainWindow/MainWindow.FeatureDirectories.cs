#nullable enable

using System;
using System.Globalization;
using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private string EnsurePresetDirectory()
    {
        var baseDirectory = _userConfiguration.GetConfigurationDirectory();
        var presetDirectory = Path.Combine(baseDirectory, PresetDirectoryName);
        Directory.CreateDirectory(presetDirectory);
        return presetDirectory;
    }

    private string EnsureBackupDirectory()
    {
        var baseDirectory = _userConfiguration.GetConfigurationDirectory();
        var backupDirectoryName = _userConfiguration.GetActiveGameProfileBackupDirectoryName();
        var backupDirectory = Path.Combine(baseDirectory, backupDirectoryName);
        Directory.CreateDirectory(backupDirectory);
        return backupDirectory;
    }

    private string EnsureLocalModBackupRootDirectory()
    {
        var backupDirectory = EnsureBackupDirectory();
        var localModsDirectory = Path.Combine(backupDirectory, "Backup Local Mods");
        Directory.CreateDirectory(localModsDirectory);
        return localModsDirectory;
    }

    private string CreateLocalModBackupSessionDirectory()
    {
        var rootDirectory = EnsureLocalModBackupRootDirectory();
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var sessionName = $"Local Mods {timestamp}";
        var sessionDirectory = Path.Combine(rootDirectory, sessionName);
        sessionDirectory = FileNameHelper.EnsureUniqueDirectoryPath(sessionDirectory);
        Directory.CreateDirectory(sessionDirectory);
        return sessionDirectory;
    }

    private string GetLocalModBackupEntryDirectory(string sessionDirectory, ModListItemViewModel mod)
    {
        var fallbackName = string.IsNullOrWhiteSpace(mod.ModId) ? "Mod" : mod.ModId;
        var displayName = string.IsNullOrWhiteSpace(mod.DisplayName) ? fallbackName : mod.DisplayName;
        var sanitized = FileNameHelper.SanitizeFileName(displayName, fallbackName);
        var entryDirectory = Path.Combine(sessionDirectory, sanitized);
        return FileNameHelper.EnsureUniqueDirectoryPath(entryDirectory);
    }

    private string EnsureModListDirectory()
    {
        var baseDirectory = _userConfiguration.GetConfigurationDirectory();
        var modListDirectory = Path.Combine(baseDirectory, ModListDirectoryName);
        Directory.CreateDirectory(modListDirectory);
        return modListDirectory;
    }

    private string EnsureRebuiltModListDirectory()
    {
        var modListBaseDirectory = EnsureModListDirectory();
        var rebuiltModListDirectory = Path.Combine(modListBaseDirectory, RebuiltModListDirectoryName);
        Directory.CreateDirectory(rebuiltModListDirectory);
        return rebuiltModListDirectory;
    }

    private string EnsureCloudModListCacheDirectory()
    {
        var baseDirectory = _userConfiguration.GetConfigurationDirectory();
        var cacheDirectory = Path.Combine(baseDirectory, CloudModListCacheDirectoryName);
        Directory.CreateDirectory(cacheDirectory);
        return cacheDirectory;
    }
}
