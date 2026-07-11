using System.IO;
using System.Text;
using System.Text.Json;
using VintageStoryModManager.Models;

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

    internal static IReadOnlyList<CloudModlistListEntry> BuildListEntries(
        IEnumerable<CloudModlistRegistryEntry>? registryEntries)
    {
        var list = new List<CloudModlistListEntry>();
        if (registryEntries is null)
            return list;

        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in registryEntries)
        {
            if (entry is null)
                continue;

            if (!seen.Add(entry.RegistryKey))
                continue;

            list.Add(CreateListEntry(entry));
        }

        list.Sort((left, right) =>
        {
            var comparison = string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);

            if (comparison != 0)
                return comparison;

            comparison = string.Compare(
                left.OwnerId,
                right.OwnerId,
                StringComparison.OrdinalIgnoreCase);

            if (comparison != 0)
                return comparison;

            return string.Compare(
                left.SlotKey,
                right.SlotKey,
                StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }

    internal static CloudModlistListEntry CreateListEntry(
        CloudModlistRegistryEntry entry,
        bool? isContentComplete = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var metadata =
            ModlistMetadataParser.ExtractModlistMetadata(
                entry.ContentJson);

        return new CloudModlistListEntry(
            entry.OwnerId,
            entry.SlotKey,
            FormatCloudSlotLabel(entry.SlotKey),
            metadata.Name,
            metadata.Description,
            metadata.Version,
            metadata.Uploader,
            metadata.Mods,
            entry.ContentJson,
            entry.DateAdded,
            metadata.GameVersion,
            isContentComplete ?? entry.IsContentComplete);
    }

    internal static string? ExtractModlistName(string? json)
        {
            return ModlistMetadataParser.ExtractModlistMetadata(json).Name;
        }

    internal static string? NormalizeCloudVersion(string? version)
        {
            return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        }
}
