using System.Diagnostics;
using System.IO;

namespace VintageStoryModManager.Services;

internal static class BackupRetentionService
{
    internal static bool IsAppStartedBackup(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var name = Path.GetFileNameWithoutExtension(path);
            return name.EndsWith("_AppStarted", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("-- AppStarted", StringComparison.OrdinalIgnoreCase);
        }

    internal static void PruneAutomaticBackups(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*.json");
                var regularBackups = files
                    .Where(file => !IsAppStartedBackup(file))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();

                if (regularBackups.Length <= 10) return;

                for (var index = 10; index < regularBackups.Length; index++)
                {
                    var candidate = regularBackups[index];
                    try
                    {
                        File.Delete(candidate);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Trace.TraceWarning("Failed to delete backup {0}: {1}", candidate, ex.Message);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Failed to prune backups in {0}: {1}", directory, ex.Message);
            }
        }

    internal static void PruneAppStartedBackups(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "*.json");
                var appStartedBackups = files
                    .Where(IsAppStartedBackup)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();

                if (appStartedBackups.Length <= 10) return;

                for (var index = 10; index < appStartedBackups.Length; index++)
                {
                    var candidate = appStartedBackups[index];
                    try
                    {
                        File.Delete(candidate);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Trace.TraceWarning("Failed to delete backup {0}: {1}", candidate, ex.Message);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Failed to prune backups in {0}: {1}", directory, ex.Message);
            }
        }
}
