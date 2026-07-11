using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class ModConfigurationCaptureService
{
    internal static ModConfigurationCaptureResult Capture(
        IReadOnlyList<ModConfigurationCaptureRequest> requests,
        string? dataDirectory)
    {
        if (requests is null ||
            requests.Count == 0)
        {
            return ModConfigurationCaptureResult.Empty;
        }

        var includedConfigurations =
            new Dictionary<string, List<ModConfigurationSnapshot>>(
                StringComparer.OrdinalIgnoreCase);

        var errors =
            new List<ModConfigurationCaptureError>();

        foreach (var request in requests)
        {
            if (request is null ||
                string.IsNullOrWhiteSpace(request.ModId) ||
                request.ConfigPaths.Count == 0)
            {
                continue;
            }

            var modId = request.ModId.Trim();
            var displayName =
                string.IsNullOrWhiteSpace(request.DisplayName)
                    ? modId
                    : request.DisplayName.Trim();

            foreach (var path in request.ConfigPaths)
            {
                try
                {
                    var content = File.ReadAllText(path);

                    var fileName =
                        ModConfigPathHelper.GetSafeConfigFileName(
                            Path.GetFileName(path),
                            modId);

                    var relativePath =
                        TryGetRelativeConfigPath(
                            dataDirectory,
                            path,
                            fileName);

                    if (!includedConfigurations.TryGetValue(
                            modId,
                            out var snapshots))
                    {
                        snapshots =
                            new List<ModConfigurationSnapshot>();

                        includedConfigurations[modId] =
                            snapshots;
                    }

                    snapshots.Add(
                        new ModConfigurationSnapshot(
                            fileName,
                            content,
                            relativePath));
                }
                catch (Exception ex) when (
                    ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
                {
                    errors.Add(
                        new ModConfigurationCaptureError(
                            modId,
                            displayName,
                            path,
                            ex.Message));
                }
            }
        }

        IReadOnlyDictionary<
            string,
            IReadOnlyList<ModConfigurationSnapshot>>? configurations =
            includedConfigurations.Count == 0
                ? null
                : includedConfigurations.ToDictionary(
                    pair => pair.Key,
                    pair =>
                        (IReadOnlyList<ModConfigurationSnapshot>)
                        pair.Value,
                    StringComparer.OrdinalIgnoreCase);

        return new ModConfigurationCaptureResult(
            configurations,
            errors);
    }

    private static string? TryGetRelativeConfigPath(
        string? dataDirectory,
        string path,
        string sanitizedFileName)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            return null;

        var configDirectory =
            Path.Combine(dataDirectory, "ModConfig");

        if (!PathRelationshipHelper.IsPathWithinDirectory(
                configDirectory,
                path))
        {
            return null;
        }

        try
        {
            var relativePath =
                Path.GetRelativePath(
                    Path.GetFullPath(configDirectory),
                    Path.GetFullPath(path));

            return ModConfigPathHelper.NormalizeRelativeConfigPath(
                relativePath,
                sanitizedFileName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal sealed record ModConfigurationCaptureRequest(
    string ModId,
    string DisplayName,
    IReadOnlyList<string> ConfigPaths);

internal sealed record ModConfigurationCaptureError(
    string ModId,
    string DisplayName,
    string Path,
    string Message);

internal sealed record ModConfigurationCaptureResult(
    IReadOnlyDictionary<
        string,
        IReadOnlyList<ModConfigurationSnapshot>>? Configurations,
    IReadOnlyList<ModConfigurationCaptureError> Errors)
{
    internal static ModConfigurationCaptureResult Empty { get; } =
        new(
            null,
            Array.Empty<ModConfigurationCaptureError>());
}
