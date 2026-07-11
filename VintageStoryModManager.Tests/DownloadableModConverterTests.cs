using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class DownloadableModConverterTests
{
    private static DownloadableModRelease CreateRelease(
        string mainFile = "https://example.invalid/mod.zip",
        string filename = "mod.zip",
        string modVersion = "1.0.0",
        string created = "2024-01-01T00:00:00Z",
        List<string>? tags = null)
    {
        return new DownloadableModRelease
        {
            MainFile = mainFile,
            Filename = filename,
            ModVersion = modVersion,
            Created = created,
            Tags = tags ?? new List<string> { "v1.20" }
        };
    }

    private static DownloadableMod CreateMod(
        int modId = 123,
        string? modIdStr = null,
        string name = "Test Mod",
        string author = "",
        string? logoFileDatabase = null,
        List<DownloadableModRelease>? releases = null)
    {
        return new DownloadableMod
        {
            ModId = modId,
            ModIdStr = modIdStr,
            AssetId = 456,
            Name = name,
            Author = author,
            LogoFileDatabase = logoFileDatabase,
            Releases = releases ?? new List<DownloadableModRelease> { CreateRelease() }
        };
    }

    [Fact]
    public void ToReleaseInfo_MissingMainFile_ReturnsNull()
    {
        var release = CreateRelease(mainFile: "");

        var result = DownloadableModConverter.ToReleaseInfo(release);

        Assert.Null(result);
    }

    [Fact]
    public void ToReleaseInfo_MissingFilename_ReturnsNull()
    {
        var release = CreateRelease(filename: "");

        var result = DownloadableModConverter.ToReleaseInfo(release);

        Assert.Null(result);
    }

    [Fact]
    public void ToReleaseInfo_ParsesCreatedIntoUtc()
    {
        var release = CreateRelease(created: "2024-03-15T12:00:00Z");

        var result = DownloadableModConverter.ToReleaseInfo(release);

        Assert.NotNull(result);
        Assert.NotNull(result!.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, result.CreatedUtc!.Value.Kind);
    }

    [Fact]
    public void ToReleaseInfo_UnparseableCreated_LeavesCreatedUtcNull()
    {
        var release = CreateRelease(created: "not-a-date");

        var result = DownloadableModConverter.ToReleaseInfo(release);

        Assert.NotNull(result);
        Assert.Null(result!.CreatedUtc);
    }

    [Fact]
    public void ToReleaseInfo_MapsVersionDownloadUriFileNameAndTags()
    {
        var release = CreateRelease(
            mainFile: "https://example.invalid/download/mod-1.0.0.zip",
            filename: "mod-1.0.0.zip",
            modVersion: "1.0.0",
            tags: new List<string> { "v1.19", "v1.20" });

        var result = DownloadableModConverter.ToReleaseInfo(release);

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result!.Version);
        Assert.Equal(new Uri("https://example.invalid/download/mod-1.0.0.zip"), result.DownloadUri);
        Assert.Equal("mod-1.0.0.zip", result.FileName);
        Assert.Equal(new[] { "v1.19", "v1.20" }, result.GameVersionTags);
    }

    [Fact]
    public void ToModEntry_PicksNewestReleaseByCreatedDateAsLatest()
    {
        var older = CreateRelease(mainFile: "https://example.invalid/old.zip", filename: "old.zip",
            modVersion: "1.0.0", created: "2023-01-01T00:00:00Z");
        var newer = CreateRelease(mainFile: "https://example.invalid/new.zip", filename: "new.zip",
            modVersion: "2.0.0", created: "2024-01-01T00:00:00Z");
        var mod = CreateMod(releases: new List<DownloadableModRelease> { older, newer });

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Equal("2.0.0", entry.DatabaseInfo!.LatestVersion);
        Assert.Single(entry.DatabaseInfo.Releases);
        Assert.Equal("new.zip", entry.DatabaseInfo.LatestRelease!.FileName);
    }

    [Fact]
    public void ToModEntry_EmptyAuthor_ProducesEmptyAuthorsList()
    {
        var mod = CreateMod(author: "");

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Empty(entry.Authors);
    }

    [Fact]
    public void ToModEntry_WhitespaceAuthor_ProducesEmptyAuthorsList()
    {
        var mod = CreateMod(author: "   ");

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Empty(entry.Authors);
    }

    [Fact]
    public void ToModEntry_ValidAuthor_ProducesSingleAuthorEntry()
    {
        var mod = CreateMod(author: "SomeAuthor");

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Equal(new[] { "SomeAuthor" }, entry.Authors);
    }

    [Fact]
    public void ToModEntry_NoValidReleases_ProducesEmptyReleasesAndNullLatestVersion()
    {
        var invalidRelease = CreateRelease(mainFile: "", filename: "");
        var mod = CreateMod(releases: new List<DownloadableModRelease> { invalidRelease });

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Empty(entry.DatabaseInfo!.Releases);
        Assert.Null(entry.DatabaseInfo.LatestVersion);
        Assert.Null(entry.DatabaseInfo.LatestRelease);
    }

    [Fact]
    public void ToModEntry_ModIdStrPresent_UsesModIdStr()
    {
        var mod = CreateMod(modId: 42, modIdStr: "custommodid");

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Equal("custommodid", entry.ModId);
    }

    [Fact]
    public void ToModEntry_ModIdStrMissing_FallsBackToNumericModIdString()
    {
        var mod = CreateMod(modId: 42, modIdStr: null);

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Equal("42", entry.ModId);
    }

    [Fact]
    public void ToModEntry_LogoFileDatabaseNonWhitespace_SetsLogoUrlSourceToLogoFileDb()
    {
        var mod = CreateMod(logoFileDatabase: "some-logo.png");

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Equal("logofiledb", entry.DatabaseInfo!.LogoUrlSource);
    }

    [Fact]
    public void ToModEntry_LogoFileDatabaseWhitespaceOrNull_LeavesLogoUrlSourceNull()
    {
        var mod = CreateMod(logoFileDatabase: null);

        var entry = DownloadableModConverter.ToModEntry(mod);

        Assert.Null(entry.DatabaseInfo!.LogoUrlSource);
    }
}
