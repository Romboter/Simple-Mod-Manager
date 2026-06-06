using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class CloudModlistManagementService
{
    internal static async Task<
        IReadOnlyList<CloudModlistManagementEntry>> LoadEntriesAsync(
        FirebaseModlistStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var slots =
            await CloudModlistSlotService.LoadSlotsAsync(
                store,
                false,
                true);

        var entries =
            new List<CloudModlistManagementEntry>(
                slots.Count);

        foreach (var slot in slots)
        {
            var slotLabel =
                CloudModlistHelper.FormatCloudSlotLabel(
                    slot.SlotKey);

            entries.Add(
                new CloudModlistManagementEntry(
                    slot.SlotKey,
                    slotLabel,
                    slot.Name,
                    slot.Version,
                    slot.DisplayName,
                    slot.CachedContent));
        }

        return entries;
    }
}
