#nullable enable

using System.IO;
using VintageStoryModManager.Helpers;

namespace VintageStoryModManager.Services;

internal static class ModlistWorkflowService
{
    public static ModlistFilePathResolution ResolveModlistFilePath(
        string directory, string? listName, string? suggestedName, string extension)
    {
        var suggestedEntryName = !string.IsNullOrWhiteSpace(listName)
            ? listName
            : suggestedName;
        var entryName = FileNameHelper.BuildSuggestedFileName(suggestedEntryName, "Modlist");
        var filePath = Path.Combine(directory, entryName + extension);

        return new ModlistFilePathResolution(entryName, filePath, File.Exists(filePath));
    }
}

internal sealed record ModlistFilePathResolution(string EntryName, string FilePath, bool AlreadyExists);
