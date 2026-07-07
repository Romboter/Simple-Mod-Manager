using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class OfflineModDatabaseInfoBuilderTests
{
    private static OfflineModDatabaseInfoBuilder CreateBuilder(string? gameVersion = null, bool exactMatch = false)
    {
        return new OfflineModDatabaseInfoBuilder(() => gameVersion, () => exactMatch);
    }

    private static ModEntry CreateEntry(
        string modId = "no-cache-mod-3f1a9c2b",
        string? version = "1.0.0",
        string? side = "both",
        IReadOnlyList<ModDependencyInfo>? dependencies = null,
        string sourcePath = @"C:\data\Mods\testmod.zip",
        ModSourceKind sourceKind = ModSourceKind.ZipArchive)
    {
        return new ModEntry
        {
            ModId = modId,
            Name = modId,
            Version = version,
            Side = side,
            Dependencies = dependencies ?? Array.Empty<ModDependencyInfo>(),
            SourcePath = sourcePath,
            SourceKind = sourceKind
        };
    }

    [Fact]
    public void CreateOfflineDatabaseInfo_NullEntry_ReturnsNull()
    {
        var builder = CreateBuilder();

        var result = builder.CreateOfflineDatabaseInfo(null!);

        Assert.Null(result);
    }

    [Fact]
    public void CreateOfflineDatabaseInfo_NoCache_SynthesizesFromEntry()
    {
        var builder = CreateBuilder();
        var dependencies = new List<ModDependencyInfo> { new("game", "1.21.0") };
        var entry = CreateEntry(version: "2.3.4", side: "client", dependencies: dependencies);

        var result = builder.CreateOfflineDatabaseInfo(entry);

        Assert.NotNull(result);
        Assert.True(result!.IsOfflineOnly);
        Assert.Equal(entry.Version, result.LatestVersion);
        Assert.Equal(entry.Side, result.Side);
        Assert.Contains("1.21.0", result.RequiredGameVersions);
    }

    [Fact]
    public void MergeOfflineAndCachedInfo_NullHandling()
    {
        var cached = new ModDatabaseInfo { LatestVersion = "1.0.0" };
        var offline = new ModDatabaseInfo { LatestVersion = "2.0.0" };

        Assert.Same(cached, OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo(null, cached));
        Assert.Same(offline, OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo(offline, null));
        Assert.Null(OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo(null, null));
    }

    [Fact]
    public void MergeOfflineAndCachedInfo_CachedTagsWin_OfflineVersionsWin()
    {
        var offline = new ModDatabaseInfo
        {
            Tags = new[] { "offline-tag" },
            AssetId = "offline-asset",
            ModPageUrl = "https://offline.invalid",
            Downloads = 1,
            LatestVersion = "2.0.0",
            LatestCompatibleVersion = "2.0.0",
            IsOfflineOnly = true
        };
        var cached = new ModDatabaseInfo
        {
            Tags = new[] { "cached-tag" },
            AssetId = "cached-asset",
            ModPageUrl = "https://cached.invalid",
            Downloads = 42,
            LatestVersion = "1.0.0",
            LatestCompatibleVersion = "1.0.0",
            IsOfflineOnly = false
        };

        var merged = OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo(offline, cached);

        Assert.NotNull(merged);
        Assert.Equal(cached.Tags, merged!.Tags);
        Assert.Equal(cached.AssetId, merged.AssetId);
        Assert.Equal(cached.ModPageUrl, merged.ModPageUrl);
        Assert.Equal(cached.Downloads, merged.Downloads);
        Assert.Equal(offline.LatestVersion, merged.LatestVersion);
        Assert.Equal(offline.LatestCompatibleVersion, merged.LatestCompatibleVersion);
        // IsOfflineOnly = offlineInfo.IsOfflineOnly && (cachedInfo?.IsOfflineOnly ?? true)
        Assert.False(merged.IsOfflineOnly);
    }

    [Fact]
    public void CreateInfoWithoutTags_StripsTags_KeepsEverythingElse()
    {
        var source = new ModDatabaseInfo
        {
            Tags = new[] { "tag1", "tag2" },
            CachedTagsVersion = "abc",
            AssetId = "asset-1",
            ModPageUrl = "https://example.invalid",
            LatestVersion = "1.0.0",
            LatestCompatibleVersion = "1.0.0",
            Downloads = 10,
            IsOfflineOnly = true,
            Side = "both"
        };

        var result = OfflineModDatabaseInfoBuilder.CreateInfoWithoutTags(source);

        Assert.Empty(result.Tags);
        Assert.Null(result.CachedTagsVersion);
        Assert.Equal(source.AssetId, result.AssetId);
        Assert.Equal(source.ModPageUrl, result.ModPageUrl);
        Assert.Equal(source.LatestVersion, result.LatestVersion);
        Assert.Equal(source.LatestCompatibleVersion, result.LatestCompatibleVersion);
        Assert.Equal(source.Downloads, result.Downloads);
        Assert.Equal(source.IsOfflineOnly, result.IsOfflineOnly);
        Assert.Equal(source.Side, result.Side);
    }

    [Fact]
    public void DetermineInstalledGameCompatibility_MinimumVersionRule()
    {
        var dependencies = new List<ModDependencyInfo> { new("game", "1.21.0") };

        Assert.True(CreateBuilder("1.21.4").DetermineInstalledGameCompatibility(dependencies));
        Assert.False(CreateBuilder("1.20.9").DetermineInstalledGameCompatibility(dependencies));
        Assert.True(CreateBuilder(null).DetermineInstalledGameCompatibility(dependencies));
        Assert.True(CreateBuilder("1.21.4").DetermineInstalledGameCompatibility(Array.Empty<ModDependencyInfo>()));
    }

    [Fact]
    public void DetermineInstalledGameCompatibility_ExactMatchMode()
    {
        var dependencies = new List<ModDependencyInfo> { new("game", "1.21.3") };

        Assert.False(CreateBuilder("1.21.4", true).DetermineInstalledGameCompatibility(dependencies));

        var exactDependencies = new List<ModDependencyInfo> { new("game", "1.21.4") };
        Assert.True(CreateBuilder("1.21.4", true).DetermineInstalledGameCompatibility(exactDependencies));
    }

    [Fact]
    public void CreateOfflineDatabaseInfo_RequiredVersionsAggregated()
    {
        var builder = CreateBuilder();
        var dependencies = new List<ModDependencyInfo>
        {
            new("game", "1.20.0"),
            new("creative", "1.21.0")
        };
        var entry = CreateEntry(dependencies: dependencies);

        var result = builder.CreateOfflineDatabaseInfo(entry);

        Assert.NotNull(result);
        Assert.Contains("1.20.0", result!.RequiredGameVersions);
        Assert.Contains("1.21.0", result.RequiredGameVersions);
        Assert.Equal(2, result.RequiredGameVersions.Count);
    }
}
