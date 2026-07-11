#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

public sealed record ModlistBackupLoadResult(ModPreset? Preset, string? ErrorMessage, bool FileMissing);

internal sealed class ModlistBackupCoordinator : IDisposable
{
    private readonly Func<string> _ensureBackupDirectory;
    private readonly SemaphoreSlim _backupSemaphore = new(1, 1);

    public ModlistBackupCoordinator(Func<string> ensureBackupDirectory)
    {
        _ensureBackupDirectory = ensureBackupDirectory;
    }

    public void Dispose()
    {
        _backupSemaphore.Dispose();
    }

    public async Task CreateBackupAsync(
        string trigger,
        string fallbackFileName,
        bool pruneAutomaticBackups,
        bool pruneAppStartedBackups,
        IReadOnlyList<ModPresetModState> modStates,
        int modCount,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
        string? gameVersion)
    {
        await _backupSemaphore.WaitAsync().ConfigureAwait(true);
        try
        {
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
                directory = _ensureBackupDirectory();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Failed to prepare backup directory: {0}", ex.Message);
                return;
            }

            var fileName = FileNameHelper.SanitizeFileName(displayName, fallbackFileName);
            var filePath = Path.Combine(directory, $"{fileName}.json");

            var serializable = PresetSnapshotBuilder.BuildSerializablePreset(
                modStates,
                displayName,
                true,
                true,
                includedConfigurations,
                gameVersion);

            var json = PdfModlistSerializer.SerializeToJson(serializable);

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

    public ModlistBackupLoadResult LoadBackupForRestore(string backupPath)
    {
        if (!File.Exists(backupPath))
            return new ModlistBackupLoadResult(null, null, FileMissing: true);

        var loaded = PresetFileLoader.TryLoadPresetFromFile(
            backupPath,
            "Backup",
            new PresetLoadOptions(true, true, true),
            out var preset,
            out var errorMessage);

        return loaded
            ? new ModlistBackupLoadResult(preset, null, FileMissing: false)
            : new ModlistBackupLoadResult(null, errorMessage, FileMissing: false);
    }

    public IReadOnlyList<string> ListBackupFiles()
    {
        var directory = _ensureBackupDirectory();
        var files = Directory.GetFiles(directory, "*.json");

        Array.Sort(files, (left, right) =>
            File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

        var result = new List<string>();
        var appStartedAdded = false;

        foreach (var file in files)
        {
            var isAppStarted = BackupRetentionService.IsAppStartedBackup(file);
            if (isAppStarted)
            {
                if (appStartedAdded) continue;
                appStartedAdded = true;
            }

            result.Add(file);
        }

        return result;
    }
}
