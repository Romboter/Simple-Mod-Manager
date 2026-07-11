#nullable enable

using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

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

    public static string BuildModlistJson(
        IReadOnlyList<ModPresetModState> modStates,
        string modlistName,
        string? description,
        string? version,
        string uploader,
        IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
        string? gameVersion)
    {
        var serializable = PresetSnapshotBuilder.BuildModlistPreset(
            modStates,
            modlistName,
            description,
            version,
            uploader,
            includedConfigurations,
            gameVersion);

        return PdfModlistSerializer.SerializeToJson(serializable);
    }
}

internal sealed record ModlistFilePathResolution(string EntryName, string FilePath, bool AlreadyExists);
