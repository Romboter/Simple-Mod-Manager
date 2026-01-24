using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Service that wraps FirebaseModlistStore and provides a simplified interface
/// for managing cloud modlists with proper initialization handling.
/// </summary>
public sealed class FirebaseModlistService : IDisposable
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private FirebaseModlistStore? _store;
    private bool _disposed;

    /// <summary>
    /// Initializes the Firebase modlist store with player identity.
    /// </summary>
    /// <param name="playerUid">The player UID from Vintage Story</param>
    /// <param name="playerName">The player name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The initialized store</returns>
    public async Task<FirebaseModlistStore> InitializeAsync(
        string? playerUid,
        string? playerName,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FirebaseModlistService));

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_store is not null)
            {
                _store.SetPlayerIdentity(playerUid, playerName);
                return _store;
            }

            var store = new FirebaseModlistStore();
            store.SetPlayerIdentity(playerUid, playerName);
            _store = store;
            return store;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Gets the initialized store. Throws if not initialized.
    /// </summary>
    public FirebaseModlistStore GetStore()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FirebaseModlistService));
        return _store ?? throw new InvalidOperationException("Firebase store not initialized. Call InitializeAsync first.");
    }

    /// <summary>
    /// Gets available slot keys for storing modlists.
    /// </summary>
    public static IReadOnlyList<string> GetSlotKeys() => FirebaseModlistStore.SlotKeys;

    /// <summary>
    /// Saves a modlist to a cloud slot.
    /// </summary>
    /// <param name="slotKey">The slot key (e.g., "slot1")</param>
    /// <param name="modlistJson">The modlist JSON content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SaveModlistAsync(string slotKey, string modlistJson, CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        await store.SaveAsync(slotKey, modlistJson, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a modlist from a cloud slot.
    /// </summary>
    /// <param name="slotKey">The slot key (e.g., "slot1")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The modlist JSON content, or null if the slot is empty</returns>
    public async Task<string?> LoadModlistAsync(string slotKey, CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        return await store.LoadAsync(slotKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a modlist from a cloud slot.
    /// </summary>
    /// <param name="slotKey">The slot key (e.g., "slot1")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task DeleteModlistAsync(string slotKey, CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        await store.DeleteAsync(slotKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all occupied slots for the current player.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of slot keys that contain modlists</returns>
    public async Task<IReadOnlyList<string>> ListSlotsAsync(CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        return await store.ListSlotsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the first available (empty) slot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first free slot key, or null if all slots are occupied</returns>
    public async Task<string?> GetFirstFreeSlotAsync(CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        return await store.GetFirstFreeSlotAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all modlists from the public registry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of registry entries</returns>
    public async Task<IReadOnlyList<CloudModlistRegistryEntry>> GetRegistryEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        return await store.GetRegistryEntriesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a specific modlist from the public registry.
    /// </summary>
    /// <param name="registryId">The registry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The registry entry, or null if not found</returns>
    public async Task<CloudModlistRegistryEntry?> GetRegistryEntryAsync(
        string registryId,
        CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        return await store.GetRegistryEntryAsync(registryId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all cloud modlists and authorization for the current player.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task DeleteAllUserDataAsync(CancellationToken cancellationToken = default)
    {
        var store = GetStore();
        await store.DeleteAllUserDataAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current user ID (player UID).
    /// </summary>
    public string? GetCurrentUserId()
    {
        return _store?.CurrentUserId;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _store?.Dispose();
        _initializationLock.Dispose();
    }
}
