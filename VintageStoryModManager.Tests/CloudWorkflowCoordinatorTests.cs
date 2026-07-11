using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CloudWorkflowCoordinatorTests
{
    private static CloudWorkflowCoordinator CreateCoordinator(
        string? playerUid = null, string? playerName = null) =>
        new(() => playerUid, () => playerName, new UserConfigurationService());

    [Fact]
    public async Task EnsureStoreInitializedAsync_FirstCall_ReturnsNewStore()
    {
        var coordinator = CreateCoordinator();

        var store = await coordinator.EnsureStoreInitializedAsync();

        Assert.NotNull(store);
        store.Dispose();
    }

    [Fact]
    public async Task EnsureStoreInitializedAsync_SecondCall_ReturnsSameInstance()
    {
        var coordinator = CreateCoordinator();

        var first = await coordinator.EnsureStoreInitializedAsync();
        var second = await coordinator.EnsureStoreInitializedAsync();

        Assert.Same(first, second);
        first.Dispose();
    }

    [Fact]
    public void ApplyPlayerIdentity_NullStore_DoesNotThrow()
    {
        var coordinator = CreateCoordinator("uid-1", "PlayerOne");

        var exception = Record.Exception(() => coordinator.ApplyPlayerIdentity(null));

        Assert.Null(exception);
    }
}
