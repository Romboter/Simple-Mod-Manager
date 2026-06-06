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
