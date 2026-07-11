using System.IO;

namespace VintageStoryModManager.Services;

internal static class ManagerDataFolderRelocationService
{
    internal static string? GetCurrentManagerFolder()
    {
        return ModCacheLocator.GetManagerDataDirectory();
    }

    internal static string? GetDefaultManagerFolder()
    {
        return ModCacheLocator.GetDefaultManagerDataDirectory();
    }

    internal static string GetCustomManagerFolder(string parentFolder)
    {
        return Path.Combine(parentFolder, "Simple VS Manager");
    }

    internal static bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    internal static bool IsSameLocation(string firstPath, string secondPath)
    {
        return string.Equals(
            firstPath,
            secondPath,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static ManagerFolderMoveResult MoveToCustomFolder(
        string currentFolder,
        string newManagerFolder)
    {
        if (IsSameLocation(currentFolder, newManagerFolder))
            return ManagerFolderMoveResult.SameLocation;

        Directory.CreateDirectory(newManagerFolder);
        DirectoryUtility.MoveDirectoryContents(
            currentFolder,
            newManagerFolder);
        CustomConfigFolderManager.SetCustomConfigFolder(newManagerFolder);

        return ManagerFolderMoveResult.Completed;
    }

    internal static void ResetToDefaultFolder(
        string currentFolder,
        string defaultFolder)
    {
        Directory.CreateDirectory(defaultFolder);
        DirectoryUtility.MoveDirectoryContents(
            currentFolder,
            defaultFolder);
        CustomConfigFolderManager.ClearCustomConfigFolder();
    }
}

internal enum ManagerFolderMoveResult
{
    Completed,
    SameLocation
}
