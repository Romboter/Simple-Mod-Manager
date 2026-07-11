using System.IO;
using System.Security;

namespace VintageStoryModManager.Services;

internal static class DirectoryUtility
{
    internal static void MoveDirectoryContents(string sourceDir, string targetDir)
        {
            // If source and target are the same, nothing to do
            if (string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase))
                return;

            // Create target directory if it doesn't exist
            Directory.CreateDirectory(targetDir);

            // Move all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);

                try
                {
                    // Use File.Move for atomic operation when possible
                    if (File.Exists(targetFile))
                    {
                        // If target exists, delete it first then move
                        File.Delete(targetFile);
                    }
                    File.Move(file, targetFile);
                }
                catch (Exception)
                {
                    // Fallback to copy if move fails (e.g., across volumes)
                    try
                    {
                        File.Copy(file, targetFile, overwrite: true);
                        File.Delete(file);
                    }
                    catch (Exception)
                    {
                        // If copy also fails, leave the file in source
                        throw;
                    }
                }
            }

            // Move all subdirectories recursively
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(directory);
                var targetSubDir = Path.Combine(targetDir, dirName);
                MoveDirectoryContents(directory, targetSubDir);
            }

            // Delete the source directory if it's now empty
            try
            {
                if (Directory.GetFiles(sourceDir).Length == 0 &&
                    Directory.GetDirectories(sourceDir).Length == 0)
                {
                    Directory.Delete(sourceDir, recursive: false);
                }
            }
            catch (Exception)
            {
                // If we can't delete the empty source folder, that's okay
            }
        }

    internal static long CalculateDirectorySize(string rootDirectory)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootDirectory);
            long totalBytes = 0;

            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();
                if (string.IsNullOrWhiteSpace(currentDirectory) || !Directory.Exists(currentDirectory)) continue;

                try
                {
                    foreach (var filePath in Directory.EnumerateFiles(currentDirectory))
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            totalBytes += fileInfo.Length;
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                        catch (SecurityException)
                        {
                        }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (SecurityException)
                {
                    continue;
                }

                try
                {
                    foreach (var directoryPath in Directory.EnumerateDirectories(currentDirectory))
                        pendingDirectories.Push(directoryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (SecurityException)
                {
                }
            }

            return totalBytes;
        }
}
