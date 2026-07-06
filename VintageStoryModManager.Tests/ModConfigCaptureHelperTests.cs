using System.IO;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModConfigCaptureHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-cap-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    private string CreateFile(string name, string content = "{}")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // --- BuildOptions ---

    [Fact]
    public void BuildOptions_IncludesOnlyModsWithExistingConfigFiles_SortedByDisplayName()
    {
        var betaCfg = CreateFile("beta.json");
        var alphaCfg = CreateFile("alpha.json");
        var paths = new Dictionary<string, IReadOnlyList<string>>
        {
            ["betamod"] = new[] { betaCfg },
            ["alphamod"] = new[] { alphaCfg },
            ["nocfgmod"] = Array.Empty<string>(),
            ["gonemod"] = new[] { Path.Combine(_dir, "deleted.json") }
        };
        var mods = new[]
        {
            TestData.CreateMod("betamod"),
            TestData.CreateMod("alphamod"),
            TestData.CreateMod("nocfgmod"),
            TestData.CreateMod("gonemod")
        };

        var options = ModConfigCaptureHelper.BuildOptions(
            mods, id => paths.TryGetValue(id, out var p) ? p : Array.Empty<string>(), true);

        Assert.Equal(2, options.Count);
        Assert.Equal("alphamod", options[0].ModId);
        Assert.Equal("betamod", options[1].ModId);
        Assert.All(options, o => Assert.True(o.IsSelected));
    }

    [Fact]
    public void BuildOptions_DeduplicatesModIdsCaseInsensitively()
    {
        var cfg = CreateFile("dup.json");
        var mods = new[] { TestData.CreateMod("dupmod"), TestData.CreateMod("DUPMOD") };

        var options = ModConfigCaptureHelper.BuildOptions(mods, _ => new[] { cfg }, false);

        var option = Assert.Single(options);
        Assert.False(option.IsSelected);
    }

    // --- CaptureConfigurations ---

    [Fact]
    public void CaptureConfigurations_NoOptions_ReturnsNullWithoutError()
    {
        var (configurations, errorMessage) =
            ModConfigCaptureHelper.CaptureConfigurations(Array.Empty<ModConfigOption>(), _dir);

        Assert.Null(configurations);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void CaptureConfigurations_ReadableConfig_ReturnsSnapshotsKeyedCaseInsensitively()
    {
        var cfg = CreateFile("testmod.json", "{\"enabled\":true}");
        var option = new ModConfigOption("testmod", "Test Mod", new[] { cfg }, true);

        var (configurations, errorMessage) =
            ModConfigCaptureHelper.CaptureConfigurations(new[] { option }, _dir);

        Assert.Null(errorMessage);
        Assert.NotNull(configurations);
        Assert.True(configurations.ContainsKey("TESTMOD")); // case-insensitive comparer preserved
        Assert.Single(configurations["testmod"]);
    }

    [Fact]
    public void CaptureConfigurations_MissingFile_ReportsErrorMessage()
    {
        var option = new ModConfigOption(
            "testmod", "Test Mod", new[] { Path.Combine(_dir, "missing.json") }, true);

        var (_, errorMessage) =
            ModConfigCaptureHelper.CaptureConfigurations(new[] { option }, _dir);

        Assert.NotNull(errorMessage);
        Assert.StartsWith("Some configuration files could not be included:", errorMessage);
        Assert.Contains("Test Mod:", errorMessage);
    }

    // --- CaptureConfigurationsForMods ---

    [Fact]
    public void CaptureConfigurationsForMods_NoMods_ReturnsNull()
    {
        var result = ModConfigCaptureHelper.CaptureConfigurationsForMods(
            Array.Empty<ModListItemViewModel>(), _ => Array.Empty<string>(), _dir);

        Assert.Null(result);
    }

    [Fact]
    public void CaptureConfigurationsForMods_DeduplicatesModIdsAndSkipsMissingConfigFiles()
    {
        var cfg = CreateFile("testmod.json", "{\"enabled\":true}");
        var mods = new[] { TestData.CreateMod("testmod"), TestData.CreateMod("TESTMOD") };

        var result = ModConfigCaptureHelper.CaptureConfigurationsForMods(
            mods,
            id => id.Equals("testmod", StringComparison.OrdinalIgnoreCase)
                ? new[] { cfg, Path.Combine(_dir, "missing.json") }
                : Array.Empty<string>(),
            _dir);

        Assert.NotNull(result);
        var snapshots = Assert.Single(result!);
        Assert.Equal("testmod", snapshots.Key, StringComparer.OrdinalIgnoreCase);
        Assert.Single(snapshots.Value);
    }
}
