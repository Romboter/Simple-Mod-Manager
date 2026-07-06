using System.IO;
using VintageStoryModManager.Services;
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
}
