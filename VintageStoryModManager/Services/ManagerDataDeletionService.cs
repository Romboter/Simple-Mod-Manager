using FileRecycleOption = Microsoft.VisualBasic.FileIO.RecycleOption;
using FileUIOption = Microsoft.VisualBasic.FileIO.UIOption;
using System.IO;
using System.Security;
using Microsoft.VisualBasic.FileIO;
using SimpleVsManager.Cloud;
using VintageStoryModManager;

namespace VintageStoryModManager.Services;

internal static class ManagerDataDeletionService
{
    internal static ManagerDeletionResult DeleteAllManagerFiles(string? dataDirectory)
        {
            var directoryCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fileCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddCandidateDirectory(directoryCandidates, ModCacheLocator.GetManagerDataDirectory());
            AddCandidateDirectory(directoryCandidates,
                TryCombineSpecialFolder(Environment.SpecialFolder.MyDocuments, "Simple VS Manager"));
            AddCandidateDirectory(directoryCandidates,
                TryCombineSpecialFolder(Environment.SpecialFolder.Personal, "Simple VS Manager"));
            AddCandidateDirectory(directoryCandidates,
                TryCombineSpecialFolder(Environment.SpecialFolder.ApplicationData, "Simple VS Manager"));
            AddCandidateDirectory(directoryCandidates,
                TryCombineSpecialFolder(Environment.SpecialFolder.LocalApplicationData, "Simple VS Manager"));
            AddCandidateDirectory(directoryCandidates,
                TryCombineSpecialFolder(Environment.SpecialFolder.UserProfile, ".simple-vs-manager"));
            AddCandidateDirectory(directoryCandidates, Path.Combine(AppContext.BaseDirectory, "Simple VS Manager"));
            AddCandidateDirectory(directoryCandidates, Path.Combine(Environment.CurrentDirectory, "Simple VS Manager"));

            if (!string.IsNullOrWhiteSpace(dataDirectory))
                AddCandidateDirectory(directoryCandidates, Path.Combine(dataDirectory!, "ModData", "SimpleVSManager"));

            AddCandidateFile(fileCandidates, FirebaseAnonymousAuthenticator.GetStateFilePath());
            AddCandidateFile(fileCandidates, Path.Combine(AppContext.BaseDirectory, "SimpleVSManagerStatus.log"));
            AddCandidateFile(fileCandidates, Path.Combine(Environment.CurrentDirectory, "SimpleVSManagerStatus.log"));

            var deletedPaths = new List<string>();
            var failedPaths = new List<string>();

            foreach (var file in fileCandidates)
                try
                {
                    if (!File.Exists(file)) continue;

                    FileSystem.DeleteFile(file, FileUIOption.OnlyErrorDialogs, FileRecycleOption.SendToRecycleBin);
                    deletedPaths.Add(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                               or SecurityException or PathTooLongException or ArgumentException)
                {
                    failedPaths.Add($"{file} ({ex.Message})");
                }

            foreach (var directory in directoryCandidates.OrderByDescending(path => path.Length))
                try
                {
                    if (!Directory.Exists(directory)) continue;

                    FileSystem.DeleteDirectory(directory, FileUIOption.OnlyErrorDialogs,
                        FileRecycleOption.SendToRecycleBin);
                    deletedPaths.Add(directory);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                               or SecurityException or PathTooLongException or ArgumentException)
                {
                    failedPaths.Add($"{directory} ({ex.Message})");
                }

            deletedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            failedPaths.Sort(StringComparer.OrdinalIgnoreCase);

            return new ManagerDeletionResult(deletedPaths, failedPaths);
        }

    private static void AddCandidateDirectory(ISet<string> directories, string? path)
        {
            var normalized = TryNormalizePath(path);
            if (normalized is null) return;

            directories.Add(normalized);
        }

    private static void AddCandidateFile(ISet<string> files, string? path)
        {
            var normalized = TryNormalizePath(path);
            if (normalized is null) return;

            files.Add(normalized);
        }

    private static string? TryCombineSpecialFolder(Environment.SpecialFolder folder, string relativePath)
        {
            var root = TryGetSpecialFolderPath(folder);
            if (string.IsNullOrWhiteSpace(root)) return null;

            try
            {
                return Path.GetFullPath(Path.Combine(root!, relativePath));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException
                                           or SecurityException)
            {
                return null;
            }
        }

    private static string? TryGetSpecialFolderPath(Environment.SpecialFolder folder)
        {
            try
            {
                var path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
                if (string.IsNullOrWhiteSpace(path)) path = Environment.GetFolderPath(folder);

                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
            catch (Exception ex) when
                (ex is PlatformNotSupportedException or InvalidOperationException or SecurityException)
            {
                return null;
            }
        }

    private static string? TryNormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException
                                           or SecurityException)
            {
                return null;
            }
        }
}
