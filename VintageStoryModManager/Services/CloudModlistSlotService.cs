using System.Net.Http;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class CloudModlistSlotService
{
    internal static async Task<IReadOnlyList<CloudModlistSlot>> LoadSlotsAsync(
        FirebaseModlistStore store,
        bool includeEmptySlots,
        bool captureContent)
    {
        ArgumentNullException.ThrowIfNull(store);

        var existing = await store.ListSlotsAsync();
        var existingSet = new HashSet<string>(
            existing,
            StringComparer.OrdinalIgnoreCase);

        var result = new List<CloudModlistSlot>(
            FirebaseModlistStore.SlotKeys.Count);

        foreach (var slotKey in FirebaseModlistStore.SlotKeys)
        {
            var isOccupied = existingSet.Contains(slotKey);
            if (!includeEmptySlots && !isOccupied)
                continue;

            string? json = null;
            var metadata = ModlistMetadata.Empty;

            if (isOccupied)
            {
                try
                {
                    json = await store.LoadAsync(slotKey);
                    metadata =
                        ModlistMetadataParser.ExtractModlistMetadata(json);
                }
                catch (Exception ex) when (
                    ex is InvalidOperationException or
                    HttpRequestException or
                    TaskCanceledException)
                {
                    StatusLogService.AppendStatus(
                        $"Failed to retrieve cloud modlist for " +
                        $"{slotKey}: {ex.Message}",
                        true);
                }
            }

            var display =
                CloudModlistHelper.BuildCloudSlotDisplay(
                    slotKey,
                    metadata,
                    isOccupied);

            var cachedContent =
                captureContent ? json : null;

            result.Add(
                new CloudModlistSlot(
                    slotKey,
                    isOccupied,
                    display,
                    metadata.Name,
                    metadata.Version,
                    cachedContent));
        }

        return result;
    }
}
