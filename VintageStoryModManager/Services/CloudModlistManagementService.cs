using System.Net.Http;
using System.Text.Json;
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

    internal static async Task<CloudModlistRenameResult> RenameAsync(
        FirebaseModlistStore store,
        CloudModlistManagementEntry entry,
        string newName)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(newName))
        {
            return new CloudModlistRenameResult(
                CloudModlistRenameStatus.InvalidName,
                null,
                null);
        }

        var trimmedName = newName.Trim();
        var json = entry.CachedContent;

        if (string.IsNullOrWhiteSpace(json))
        {
            try
            {
                json =
                    await store.LoadAsync(
                        entry.SlotKey);
            }
            catch (Exception ex) when (
                ex is HttpRequestException or
                TaskCanceledException)
            {
                return new CloudModlistRenameResult(
                    CloudModlistRenameStatus.LoadFailed,
                    null,
                    ex.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new CloudModlistRenameResult(
                CloudModlistRenameStatus.ContentUnavailable,
                null,
                null);
        }

        string updatedJson;
        try
        {
            updatedJson =
                CloudModlistHelper.ReplaceCloudModlistName(
                    json,
                    trimmedName);
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException)
        {
            return new CloudModlistRenameResult(
                CloudModlistRenameStatus.InvalidContent,
                null,
                ex.Message);
        }

        try
        {
            await store.SaveAsync(
                entry.SlotKey,
                updatedJson);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            TaskCanceledException)
        {
            return new CloudModlistRenameResult(
                CloudModlistRenameStatus.SaveFailed,
                null,
                ex.Message);
        }

        return new CloudModlistRenameResult(
            CloudModlistRenameStatus.Success,
            trimmedName,
            null);
    }

    internal static async Task<CloudModlistDeleteResult> DeleteAsync(
        FirebaseModlistStore store,
        CloudModlistManagementEntry entry)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            await store.DeleteAsync(entry.SlotKey);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            TaskCanceledException)
        {
            return new CloudModlistDeleteResult(
                CloudModlistDeleteStatus.NetworkFailed,
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new CloudModlistDeleteResult(
                CloudModlistDeleteStatus.InvalidRequest,
                ex.Message);
        }

        return new CloudModlistDeleteResult(
            CloudModlistDeleteStatus.Success,
            null);
    }
}

internal enum CloudModlistRenameStatus
{
    Success,
    InvalidName,
    LoadFailed,
    ContentUnavailable,
    InvalidContent,
    SaveFailed
}

internal sealed record CloudModlistRenameResult(
    CloudModlistRenameStatus Status,
    string? Name,
    string? ErrorMessage);

internal enum CloudModlistDeleteStatus
{
    Success,
    NetworkFailed,
    InvalidRequest
}

internal sealed record CloudModlistDeleteResult(
    CloudModlistDeleteStatus Status,
    string? ErrorMessage);
