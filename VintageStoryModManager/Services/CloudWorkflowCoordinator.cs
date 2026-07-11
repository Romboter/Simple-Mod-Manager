#nullable enable

using SimpleVsManager.Cloud;

namespace VintageStoryModManager.Services;

public enum CloudMigrationOutcome
{
    AlreadyAttempted,
    NoPlayerUid,
    NotNeeded,
    Migrated
}

public sealed class CloudWorkflowCoordinator : IDisposable
{
    private readonly Func<string?> _playerUidProvider;
    private readonly Func<string?> _playerNameProvider;
    private readonly UserConfigurationService _userConfiguration;
    private readonly SemaphoreSlim _storeLock = new(1, 1);

    private FirebaseModlistStore? _store;
    private bool _migrationAttempted;

    public CloudWorkflowCoordinator(
        Func<string?> playerUidProvider,
        Func<string?> playerNameProvider,
        UserConfigurationService userConfiguration)
    {
        _playerUidProvider = playerUidProvider ?? throw new ArgumentNullException(nameof(playerUidProvider));
        _playerNameProvider = playerNameProvider ?? throw new ArgumentNullException(nameof(playerNameProvider));
        _userConfiguration = userConfiguration ?? throw new ArgumentNullException(nameof(userConfiguration));
    }

    public FirebaseModlistStore? CurrentStore => _store;

    public void ApplyPlayerIdentity(FirebaseModlistStore? store)
    {
        store?.SetPlayerIdentity(_playerUidProvider(), _playerNameProvider());
    }

    /// <summary>
    /// Drops the cached store reference without disposing it, forcing the next
    /// <see cref="EnsureStoreInitializedAsync"/> call to construct a fresh one. Used after cloud
    /// authorization/account data is deleted (matches the pre-extraction MainWindow behavior of
    /// setting the field to null without disposing the previous instance).
    /// </summary>
    public void ResetStore()
    {
        _store = null;
    }

    public void Dispose()
    {
        _storeLock.Dispose();
        _store?.Dispose();
    }

    public async Task<CloudMigrationOutcome> MigrateLegacyFirebaseDataIfNeededAsync()
    {
        if (_migrationAttempted) return CloudMigrationOutcome.AlreadyAttempted;

        _migrationAttempted = true;

        var playerUid = _playerUidProvider();
        if (string.IsNullOrWhiteSpace(playerUid)) return CloudMigrationOutcome.NoPlayerUid;

        var migrationService = new FirebaseModlistMigrationService();
        var migrationSucceeded = await migrationService
            .TryMigrateAsync(playerUid, _playerNameProvider(), _userConfiguration, CancellationToken.None)
            .ConfigureAwait(false);

        return migrationSucceeded ? CloudMigrationOutcome.Migrated : CloudMigrationOutcome.NotNeeded;
    }

    public async Task<FirebaseModlistStore> EnsureStoreInitializedAsync()
    {
        if (_store is { } existingStore)
        {
            ApplyPlayerIdentity(existingStore);
            return existingStore;
        }

        await _storeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_store is { } cached)
            {
                ApplyPlayerIdentity(cached);
                return cached;
            }

            // Migration is only attempted once (see _migrationAttempted). The dialog for a
            // successful migration is shown from the primary call in MainWindow_Loaded, not here.
            await MigrateLegacyFirebaseDataIfNeededAsync().ConfigureAwait(false);

            var store = new FirebaseModlistStore();
            ApplyPlayerIdentity(store);
            _store = store;
            return store;
        }
        finally
        {
            _storeLock.Release();
        }
    }
}
