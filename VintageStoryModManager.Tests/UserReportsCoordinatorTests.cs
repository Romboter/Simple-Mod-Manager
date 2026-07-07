using System.IO;
using System.Text.Json;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UserReportsCoordinatorTests : IDisposable
{
    private readonly List<string> _cachePaths = new();

    public void Dispose()
    {
        foreach (var path in _cachePaths)
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; leftover temp files are harmless.
            }
    }

    private sealed class StubDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private string NewCachePath()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _cachePaths.Add(path);
        return path;
    }

    private static UserReportsCoordinator CreateCoordinator(
        string cachePath,
        Func<IDisposable>? beginBusyScope = null,
        string? gameVersion = "1.20.0",
        IEnumerable<ModListItemViewModel>? installedMods = null,
        IEnumerable<ModListItemViewModel>? searchResultMods = null,
        bool allowModDetailsRefresh = true)
    {
        return new UserReportsCoordinator(
            cachePath,
            beginBusyScope ?? (() => new StubDisposable()),
            () => gameVersion,
            () => installedMods ?? Array.Empty<ModListItemViewModel>(),
            () => searchResultMods ?? Array.Empty<ModListItemViewModel>(),
            () => allowModDetailsRefresh);
    }

    [Fact]
    public void BuildVoteEtagKey_ComposesDeterministically()
    {
        var key = UserReportsCoordinator.BuildVoteEtagKey("latest", "mod1", "2.0.0", "1.19.0");

        Assert.Equal("latest|mod1|2.0.0|1.19.0", key);
    }

    [Fact]
    public void BuildVoteEtagKey_DiffersByOrdinalCase_ButIsEqualUnderOrdinalIgnoreCase()
    {
        var key1 = UserReportsCoordinator.BuildVoteEtagKey("current", "ModA", "1.0.0", "1.20.0");
        var key2 = UserReportsCoordinator.BuildVoteEtagKey("current", "moda", "1.0.0", "1.20.0");

        // The keys are literally different strings...
        Assert.NotEqual(key1, key2, StringComparer.Ordinal);

        // ...but the etag dictionaries use an OrdinalIgnoreCase comparer, so they are treated as the
        // same entry - which is what actually matters for the cache's correctness.
        Assert.Equal(key1, key2, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetVisibility_SecondIdenticalCall_ReturnsFalse()
    {
        using var coordinator = CreateCoordinator(NewCachePath());

        Assert.True(coordinator.SetVisibility(false));
        Assert.False(coordinator.SetVisibility(false));
    }

    [Fact]
    public void SetVisibility_True_DoesNotItselfTriggerAnyRefresh()
    {
        var busyScopeCalls = 0;
        using var coordinator = CreateCoordinator(NewCachePath(), beginBusyScope: () =>
        {
            busyScopeCalls++;
            return new StubDisposable();
        });

        coordinator.SetVisibility(false);
        var changed = coordinator.SetVisibility(true);

        Assert.True(changed);
        Assert.Equal(0, busyScopeCalls);
    }

    [Fact]
    public void QueueUserReportRefresh_WithNullMod_IsNoOp()
    {
        var busyScopeCalls = 0;
        using var coordinator = CreateCoordinator(NewCachePath(), beginBusyScope: () =>
        {
            busyScopeCalls++;
            return new StubDisposable();
        });

        coordinator.QueueUserReportRefresh(null!);

        Assert.Equal(0, busyScopeCalls);
    }

    [Fact]
    public void QueueUserReportRefresh_WhileInvisible_IsNoOp()
    {
        var busyScopeCalls = 0;
        using var coordinator = CreateCoordinator(NewCachePath(), beginBusyScope: () =>
        {
            busyScopeCalls++;
            return new StubDisposable();
        });
        coordinator.SetVisibility(false);

        var mod = TestData.CreateMod();
        coordinator.QueueUserReportRefresh(mod);

        Assert.Equal(0, busyScopeCalls);
    }

    [Fact]
    public void Constructor_WithMissingCacheFile_DoesNotThrow()
    {
        var path = NewCachePath();

        var exception = Record.Exception(() => CreateCoordinator(path));

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithCorruptCacheFile_DoesNotThrow()
    {
        var path = NewCachePath();
        File.WriteAllText(path, "{ not valid json ");

        var exception = Record.Exception(() => CreateCoordinator(path));

        Assert.Null(exception);
    }

    [Fact]
    public void StoreUserReportEtag_PersistsToDisk_AndIsLoadedByASecondCoordinatorOnTheSamePath()
    {
        var path = NewCachePath();
        var coordinator1 = CreateCoordinator(path);

        coordinator1.StoreUserReportEtag("mod1", "1.0.0", "etag-value");
        coordinator1.StoreLatestReleaseUserReportEtag("mod1", "2.0.0", "etag-latest");
        coordinator1.Dispose();

        Assert.True(File.Exists(path));

        var currentKey = UserReportsCoordinator.BuildVoteEtagKey("current", "mod1", "1.0.0", "1.20.0");
        var latestKey = UserReportsCoordinator.BuildVoteEtagKey("latest", "mod1", "2.0.0", "1.20.0");

        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("etag-value", document.RootElement.GetProperty("Current").GetProperty(currentKey).GetString());
            Assert.Equal("etag-latest", document.RootElement.GetProperty("Latest").GetProperty(latestKey).GetString());
        }

        // Round-trip check: a second coordinator constructed on the same cache path loads the etags
        // from disk on construction. Storing a null etag for the same mod/version deletes the entry
        // only if it was actually present in memory - which only happens if the load succeeded - so
        // observing its removal from the persisted file indirectly proves the round-trip worked.
        var coordinator2 = CreateCoordinator(path);
        coordinator2.StoreUserReportEtag("mod1", "1.0.0", null);
        coordinator2.Dispose();

        using var documentAfterRemoval = JsonDocument.Parse(File.ReadAllText(path));
        Assert.False(documentAfterRemoval.RootElement.GetProperty("Current").TryGetProperty(currentKey, out _));

        // The untouched "latest" entry survives the rewrite, confirming the persisted state wasn't
        // simply wiped wholesale.
        Assert.Equal(
            "etag-latest",
            documentAfterRemoval.RootElement.GetProperty("Latest").GetProperty(latestKey).GetString());
    }
}
