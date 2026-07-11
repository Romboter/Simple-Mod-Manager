using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModConfigDiscoveryServiceTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("smm-cfg-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dataDir, true);
    }

    private string CreateConfigFile(string fileName)
    {
        var configDir = Path.Combine(_dataDir, "ModConfig");
        Directory.CreateDirectory(configDir);
        var path = Path.Combine(configDir, fileName);
        File.WriteAllText(path, "{}");
        return path;
    }

    // --- GetScanBlockedMessage ---

    [Fact]
    public void GetScanBlockedMessage_ModsNotLoaded_ReportsLoadMods()
    {
        var message = ModConfigDiscoveryService.GetScanBlockedMessage(false, false, _dataDir);
        Assert.Equal("Mods have not been loaded yet. Load mods before scanning for configuration files.", message);
    }

    [Fact]
    public void GetScanBlockedMessage_Busy_ReportsWait()
    {
        var message = ModConfigDiscoveryService.GetScanBlockedMessage(true, true, _dataDir);
        Assert.Equal("Please wait for the current operation to finish before scanning for configuration files.", message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void GetScanBlockedMessage_NoDataDirectory_ReportsDataDirectory(string? dataDirectory)
    {
        var message = ModConfigDiscoveryService.GetScanBlockedMessage(true, false, dataDirectory);
        Assert.Equal("The Vintage Story data directory is not set, so mod configuration files cannot be located.", message);
    }

    [Fact]
    public void GetScanBlockedMessage_MissingConfigDirectory_ReportsPath()
    {
        var message = ModConfigDiscoveryService.GetScanBlockedMessage(true, false, _dataDir);
        Assert.Equal($"No mod configuration directory was found at:\n{Path.Combine(_dataDir, "ModConfig")}", message);
    }

    [Fact]
    public void GetScanBlockedMessage_AllPreconditionsMet_ReturnsNull()
    {
        CreateConfigFile("anything.json");
        Assert.Null(ModConfigDiscoveryService.GetScanBlockedMessage(true, false, _dataDir));
    }

    // --- ScanAsync ---

    [Fact]
    public async Task ScanAsync_AssignsMatchingConfigFile()
    {
        var configPath = CreateConfigFile("testmod.json");
        var stored = new Dictionary<string, string>();

        var results = await ModConfigDiscoveryService.ScanAsync(
            _dataDir,
            new (string? ModId, string? DisplayName)[] { ("testmod", "Test Mod") },
            _ => null,
            (id, path) => stored[id] = path);

        var result = Assert.Single(results);
        Assert.Equal("testmod", result.ModId);
        Assert.Equal("Test Mod", result.DisplayName);
        Assert.Equal(configPath, result.ConfigPath);
        Assert.Equal(configPath, stored["testmod"]);
    }

    [Fact]
    public async Task ScanAsync_SkipsModsThatAlreadyHaveAConfigPath()
    {
        CreateConfigFile("testmod.json");
        var setCalled = false;

        var results = await ModConfigDiscoveryService.ScanAsync(
            _dataDir,
            new (string? ModId, string? DisplayName)[] { ("testmod", "Test Mod") },
            _ => @"C:\already\assigned.json",
            (_, _) => setCalled = true);

        Assert.Empty(results);
        Assert.False(setCalled);
    }

    [Fact]
    public async Task ScanAsync_BlankDataDirectoryOrNoCandidates_ReturnsEmpty()
    {
        Assert.Empty(await ModConfigDiscoveryService.ScanAsync(
            null, new (string? ModId, string? DisplayName)[] { ("testmod", null) }, _ => null, (_, _) => { }));

        CreateConfigFile("testmod.json");
        Assert.Empty(await ModConfigDiscoveryService.ScanAsync(
            _dataDir, Array.Empty<(string? ModId, string? DisplayName)>(), _ => null, (_, _) => { }));
    }

    [Fact]
    public async Task ScanAsync_DisplayNameFallsBackToModId()
    {
        CreateConfigFile("testmod.json");

        var results = await ModConfigDiscoveryService.ScanAsync(
            _dataDir,
            new (string? ModId, string? DisplayName)[] { ("testmod", "  ") },
            _ => null,
            (_, _) => { });

        Assert.Equal("testmod", Assert.Single(results).DisplayName);
    }

    [Fact]
    public async Task ScanAsync_DeduplicatesCandidateIdsAndSkipsBlankIds()
    {
        CreateConfigFile("testmod.json");
        var setCount = 0;

        var results = await ModConfigDiscoveryService.ScanAsync(
            _dataDir,
            new (string? ModId, string? DisplayName)[] { ("testmod", "A"), ("TESTMOD", "B"), ("  ", "C"), (null, "D") },
            _ => null,
            (_, _) => setCount++);

        Assert.Single(results);
        Assert.Equal(1, setCount);
    }

    [Fact]
    public async Task ScanAsync_UnrelatedConfigFile_MatchesNothing()
    {
        CreateConfigFile("zzz-completely-unrelated-settings.json");

        var results = await ModConfigDiscoveryService.ScanAsync(
            _dataDir,
            new (string? ModId, string? DisplayName)[] { ("carrycapacity", "Carry Capacity") },
            _ => null,
            (_, _) => { });

        Assert.Empty(results);
    }

    // --- FormatAssignedConfigsMessage ---

    [Fact]
    public void FormatAssignedConfigsMessage_ShowsModIdOnlyWhenItDiffersFromDisplayName()
    {
        var message = ModConfigDiscoveryService.FormatAssignedConfigsMessage(new[]
        {
            new ModConfigScanResult("bmod", "Beta Mod", @"C:\cfg\beta.json"),
            new ModConfigScanResult("amod", "amod", @"C:\cfg\amod.json")
        });

        var expected =
            "Assigned configuration files for the following mods:" + Environment.NewLine +
            " • amod" + Environment.NewLine +
            "    amod.json" + Environment.NewLine +
            " • Beta Mod (bmod)" + Environment.NewLine +
            "    beta.json" + Environment.NewLine;
        Assert.Equal(expected, message);
    }
}
