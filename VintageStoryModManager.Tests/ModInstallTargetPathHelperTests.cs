using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModInstallTargetPathHelperTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("smm-inst-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dataDir, true);
    }

    [Fact]
    public void TryGetInstallTargetPath_NullDataDirectory_Fails()
    {
        var ok = ModInstallTargetPathHelper.TryGetInstallTargetPath(
            null, TestData.CreateMod(), TestData.CreateRelease(), out _, out var error);

        Assert.False(ok);
        Assert.Contains("VintagestoryData folder is not available", error);
    }

    [Fact]
    public void TryGetInstallTargetPath_CreatesModsDirectoryAndBuildsPath()
    {
        var ok = ModInstallTargetPathHelper.TryGetInstallTargetPath(
            _dataDir, TestData.CreateMod(), TestData.CreateRelease(), out var fullPath, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(Path.Combine(_dataDir, "Mods", "testmod_1.2.3.zip"), fullPath);
        Assert.True(Directory.Exists(Path.Combine(_dataDir, "Mods")));
    }

    [Fact]
    public void TryGetInstallTargetPath_ExistingFile_GetsUniqueSuffix()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        File.WriteAllText(Path.Combine(modsDir, "testmod_1.2.3.zip"), "");

        var ok = ModInstallTargetPathHelper.TryGetInstallTargetPath(
            _dataDir, TestData.CreateMod(), TestData.CreateRelease(), out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(Path.Combine(modsDir, "testmod_1.2.3 (1).zip"), fullPath);
    }

    [Fact]
    public void TryGetDependencyInstallTargetPath_NoFileName_FallsBackToModIdDashVersion()
    {
        var ok = ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(
            _dataDir, "depmod", TestData.CreateRelease(fileName: null), out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(Path.Combine(_dataDir, "Mods", "depmod-1.2.3.zip"), fullPath);
    }

    [Fact]
    public void TryGetDependencyInstallTargetPath_BlankModId_FallsBackToLiteralMod()
    {
        var ok = ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(
            _dataDir, "  ", TestData.CreateRelease(fileName: null), out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(Path.Combine(_dataDir, "Mods", "mod-1.2.3.zip"), fullPath);
    }
}
