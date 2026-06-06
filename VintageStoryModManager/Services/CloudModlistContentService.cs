using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class CloudModlistContentService
{
    internal static async Task<CloudModlistListEntry?> EnsureContentAsync(
        FirebaseModlistStore store,
        CloudModlistListEntry entry)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsContentComplete &&
            !string.IsNullOrWhiteSpace(entry.ContentJson))
        {
            return entry;
        }

        var registryEntry =
            await store.GetRegistryEntryAsync(entry.OwnerId);

        if (registryEntry is null ||
            string.IsNullOrWhiteSpace(registryEntry.ContentJson))
        {
            return null;
        }

        return CloudModlistHelper.CreateListEntry(
            registryEntry,
            true);
    }
}
