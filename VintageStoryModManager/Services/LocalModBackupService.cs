using System.IO;
using VintageStoryModManager.Helpers;

namespace VintageStoryModManager.Services;

internal static class LocalModBackupService
{
    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                var targetDirectory = Path.Combine(destinationDirectory, relativePath);
                Directory.CreateDirectory(targetDirectory);
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var targetPath = Path.Combine(destinationDirectory, relativePath);
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                File.Copy(file, targetPath, false);
            }
        }

    internal static void BackupLocalModAtPath(string sourcePath, string destinationDirectory)
        {
            if (Directory.Exists(sourcePath))
            {
                CopyDirectoryContents(sourcePath, destinationDirectory);
                return;
            }

            if (File.Exists(sourcePath))
            {
                Directory.CreateDirectory(destinationDirectory);
                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(destinationDirectory, fileName);
                targetPath = FileNameHelper.EnsureUniqueFilePath(targetPath);
                File.Copy(sourcePath, targetPath, false);
            }
        }
}
