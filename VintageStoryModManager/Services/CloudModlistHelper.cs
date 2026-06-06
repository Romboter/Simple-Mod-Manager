using System.IO;
using System.Text;
using System.Text.Json;
using VintageStoryModManager;

namespace VintageStoryModManager.Services;

internal static class CloudModlistHelper
{
    internal static string ReplaceCloudModlistName(string json, string newName)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("The cloud modlist content is not a valid object.");

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                var nameWritten = false;

                foreach (var property in document.RootElement.EnumerateObject())
                    if (property.NameEquals("name"))
                    {
                        writer.WriteString("name", newName);
                        nameWritten = true;
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }

                if (!nameWritten) writer.WriteString("name", newName);

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

    internal static string BuildCloudSlotDisplay(string slotKey, ModlistMetadata metadata, bool isOccupied)
        {
            if (!isOccupied) return $"{FormatCloudSlotLabel(slotKey)} (Empty)";

            var name = metadata.Name ?? "Unnamed Modlist";
            return string.IsNullOrWhiteSpace(metadata.Version)
                ? name
                : $"{name} (v{metadata.Version})";
        }

    internal static string FormatCloudSlotLabel(string slotKey)
        {
            if (string.Equals(slotKey, "public", StringComparison.OrdinalIgnoreCase)) return "Public Entry";

            if (slotKey.Length > 4 && slotKey.StartsWith("slot", StringComparison.OrdinalIgnoreCase))
                return $"Slot {slotKey.AsSpan(4).ToString()}";

            return slotKey;
        }

    internal static string? ExtractModlistName(string? json)
        {
            return ModlistMetadataParser.ExtractModlistMetadata(json).Name;
        }
}
