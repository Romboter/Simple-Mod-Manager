using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class CloudModlistSaveService
{
    internal static async Task<CloudModlistSavePlan> PrepareSaveAsync(
        FirebaseModlistStore store,
        string modlistName)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (string.IsNullOrWhiteSpace(modlistName))
            throw new ArgumentException(
                "A modlist name is required.",
                nameof(modlistName));

        var slots =
            await CloudModlistSlotService.LoadSlotsAsync(
                store,
                true,
                false);

        var trimmedName = modlistName.Trim();

        var matchingSlot =
            slots.FirstOrDefault(slot =>
                slot.IsOccupied &&
                string.Equals(
                    (slot.Name ?? string.Empty).Trim(),
                    trimmedName,
                    StringComparison.OrdinalIgnoreCase));

        var freeSlot =
            matchingSlot is null
                ? slots.FirstOrDefault(slot => !slot.IsOccupied)
                : null;

        return new CloudModlistSavePlan(
            slots,
            matchingSlot,
            freeSlot);
    }

    internal static async Task SaveAsync(
        FirebaseModlistStore store,
        string slotKey,
        string json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (string.IsNullOrWhiteSpace(slotKey))
            throw new ArgumentException(
                "A cloud slot key is required.",
                nameof(slotKey));

        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException(
                "Cloud modlist JSON is required.",
                nameof(json));

        await store.SaveAsync(
            slotKey,
            json,
            cancellationToken);
    }
}

internal sealed record CloudModlistSavePlan(
    IReadOnlyList<CloudModlistSlot> Slots,
    CloudModlistSlot? MatchingSlot,
    CloudModlistSlot? FreeSlot);
