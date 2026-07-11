#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

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
}
