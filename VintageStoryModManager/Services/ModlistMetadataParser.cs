using System.Text.Json;

namespace VintageStoryModManager.Services;

internal static class ModlistMetadataParser
{
    internal static ModlistMetadata ExtractModlistMetadata(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return ModlistMetadata.Empty;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object) return ModlistMetadata.Empty;

                var root = document.RootElement;
                var name = TryGetTrimmedProperty(root, "name");
                var description = TryGetTrimmedProperty(root, "description");
                var version = TryGetTrimmedProperty(root, "version");
                var uploader = TryGetTrimmedProperty(root, "uploader")
                               ?? TryGetTrimmedProperty(root, "uploaderName");
                var gameVersion = TryGetTrimmedProperty(root, "gameVersion")
                                  ?? TryGetTrimmedProperty(root, "vsVersion");

                var mods = new List<string>();
                if (root.TryGetProperty("mods", out var modsElement) && modsElement.ValueKind == JsonValueKind.Array)
                    foreach (var modElement in modsElement.EnumerateArray())
                    {
                        if (modElement.ValueKind != JsonValueKind.Object) continue;

                        var modId = TryGetTrimmedProperty(modElement, "modId");
                        if (string.IsNullOrWhiteSpace(modId)) continue;

                        var modVersion = TryGetTrimmedProperty(modElement, "version");
                        var display = modId;
                        if (!string.IsNullOrWhiteSpace(modVersion)) display += $" ({modVersion})";

                        mods.Add(display);
                    }

                return new ModlistMetadata(name, description, version, uploader, mods, gameVersion);
            }
            catch (JsonException)
            {
                return ModlistMetadata.Empty;
            }
        }

    private static string? TryGetTrimmedProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
                if (property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                }

            return null;
        }
}
