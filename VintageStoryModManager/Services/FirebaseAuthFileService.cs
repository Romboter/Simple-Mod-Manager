using SimpleVsManager.Cloud;
using System.IO;
using System.Security;

namespace VintageStoryModManager.Services;

internal static class FirebaseAuthFileService
{
    internal static bool HasFirebaseAuthStateFile()
        {
            var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
            return !string.IsNullOrWhiteSpace(stateFilePath) && File.Exists(stateFilePath);
        }

    internal static FirebaseAuthRestoreResult RestoreFirebaseAuthBackup()
    {
        var backupPath = FirebaseAnonymousAuthenticator.GetBackupFilePath();
        if (string.IsNullOrWhiteSpace(backupPath))
            return FirebaseAuthRestoreResult.BackupLocationUnavailable;

        if (!File.Exists(backupPath))
            return FirebaseAuthRestoreResult.BackupNotFound;

        var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
        if (string.IsNullOrWhiteSpace(stateFilePath))
            return FirebaseAuthRestoreResult.StateLocationUnavailable;

        var stateDirectory = Path.GetDirectoryName(stateFilePath);
        if (!string.IsNullOrWhiteSpace(stateDirectory))
            Directory.CreateDirectory(stateDirectory);

        File.Copy(backupPath, stateFilePath, true);

        return FirebaseAuthRestoreResult.Restored;
    }

    internal static string GetDataDirectoryBackupPath(string dataDirectory)
    {
        return Path.Combine(dataDirectory, "ModData", "SimpleVSManager", "firebase-auth.json");
    }

    internal static void BackupAuthStateToDataDirectory(string stateFilePath, string dataDirectory)
    {
        var backupPath = GetDataDirectoryBackupPath(dataDirectory);
        var backupDirectory = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrWhiteSpace(backupDirectory))
            Directory.CreateDirectory(backupDirectory);

        File.Copy(stateFilePath, backupPath, true);
    }

    internal static void TryDeleteFirebaseAuthFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                           or SecurityException)
            {
                StatusLogService.AppendStatus($"Failed to delete Firebase auth file {path}: {ex.Message}", true);
            }
        }
}

internal enum FirebaseAuthRestoreResult
{
    Restored,
    BackupLocationUnavailable,
    BackupNotFound,
    StateLocationUnavailable
}
