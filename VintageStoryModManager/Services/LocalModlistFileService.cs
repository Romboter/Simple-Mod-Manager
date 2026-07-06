using System.IO;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class LocalModlistFileService
{
    internal static LocalModlistDeletionResult Delete(
        IReadOnlyList<LocalModlistListEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var errors = new List<string>();
        var deletedCount = 0;

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath))
                continue;

            try
            {
                if (!File.Exists(entry.FilePath))
                    continue;

                File.Delete(entry.FilePath);
                deletedCount++;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{entry.DisplayName}: {ex.Message}");
            }
        }

        return new LocalModlistDeletionResult(
            deletedCount,
            errors);
    }

    internal static LocalModlistUpdateResult UpdateMetadata(
        string filePath,
        string? name,
        string? description,
        string? version,
        string? gameVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string json;

        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return LocalModlistUpdateResult.Failed(
                LocalModlistUpdateFailureStage.Read,
                ex.Message);
        }

        if (!PdfModlistSerializer.TryDeserializeFromJson(
                json,
                out var preset,
                out var errorMessage) ||
            preset is null)
        {
            var message = string.IsNullOrWhiteSpace(errorMessage)
                ? "The modlist could not be read."
                : errorMessage!;

            return LocalModlistUpdateResult.Failed(
                LocalModlistUpdateFailureStage.Parse,
                message);
        }

        preset.Name = name;
        preset.Description = description;
        preset.Version = version;
        preset.GameVersion = gameVersion;

        try
        {
            var updatedJson =
                PdfModlistSerializer.SerializeToJson(preset);

            File.WriteAllText(filePath, updatedJson);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return LocalModlistUpdateResult.Failed(
                LocalModlistUpdateFailureStage.Write,
                ex.Message);
        }

        return LocalModlistUpdateResult.Succeeded;
    }

    internal static LocalModlistSaveResult Save(string filePath, SerializablePreset preset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(preset);

        try
        {
            var json = PdfModlistSerializer.SerializeToJson(preset);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                      or PathTooLongException)
        {
            return LocalModlistSaveResult.Failed(ex.Message);
        }

        return LocalModlistSaveResult.Succeeded;
    }
}

internal sealed record LocalModlistDeletionResult(
    int DeletedCount,
    IReadOnlyList<string> Errors);

internal enum LocalModlistUpdateFailureStage
{
    None,
    Read,
    Parse,
    Write
}

internal sealed record LocalModlistUpdateResult(
    bool Success,
    LocalModlistUpdateFailureStage FailureStage,
    string? ErrorMessage)
{
    internal static LocalModlistUpdateResult Succeeded { get; } =
        new(
            true,
            LocalModlistUpdateFailureStage.None,
            null);

    internal static LocalModlistUpdateResult Failed(
        LocalModlistUpdateFailureStage failureStage,
        string errorMessage)
    {
        return new LocalModlistUpdateResult(
            false,
            failureStage,
            errorMessage);
    }
}

internal sealed record LocalModlistSaveResult(
    bool Success,
    string? ErrorMessage)
{
    internal static LocalModlistSaveResult Succeeded { get; } =
        new(
            true,
            null);

    internal static LocalModlistSaveResult Failed(string errorMessage)
    {
        return new LocalModlistSaveResult(
            false,
            errorMessage);
    }
}
